using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>What must agree across an exit (docs/gameplay.md#leaving-a-room), one closed set: the rule a seam problem breaks.</summary>
public enum SeamRule
{
    /// <summary>The opening is as wide (a hatch) or as tall (a doorway) on both sides.</summary>
    Opening,

    /// <summary>Around the opening, what both rooms show is solid, water or open in both.</summary>
    Solids,

    /// <summary>Water rests on a floor, or continues through a hole into water on the other side.</summary>
    Water,

    /// <summary>A lift that rises through a hatch has its car over the matching hatch on the other side.</summary>
    Lift,
}

/// <summary>A room as the seam check sees it, on its play plane (room positions): its camera bounds, solids, water, lifts and exits.</summary>
public sealed record SeamRoom(Box Bounds, List<Box> Solids, List<(string Name, Box Region)> Waters, List<LiftPath> Lifts, Dictionary<string, Threshold> Exits);

/// <summary>
/// One disagreement: the seam it lies at (<c>Room:Exit | Room:Exit</c>, the upper or western room first; a room's name for its own
/// water), the rule it breaks, and what disagrees in words (positions in the first room's).
/// </summary>
public sealed record SeamProblem(string Seam, SeamRule Rule, string Detail);

/// <summary>
/// Seams that don't agree yet, each written down with its reason (a world's seam exceptions, a `.tsv` of columns seam, rule, reason):
/// the seam check accepts exactly these, and an entry nothing breaks any more is a problem too, so the list only shrinks.
/// </summary>
public sealed record SeamExceptions(IReadOnlyList<(string Seam, SeamRule Rule, string Reason)> Entries)
{
    private static readonly string[] Columns = { "seam", "rule", "reason" };

    /// <summary>No exceptions: every seam agrees.</summary>
    public static SeamExceptions None { get; } = new(Array.Empty<(string, SeamRule, string)>());

    /// <summary>The exceptions in <paramref name="text"/> (blank lines and `#` comments skipped; a header row first).</summary>
    /// <exception cref="FormatException">A row without its three cells, an unknown rule or an empty reason, naming its line.</exception>
    public static SeamExceptions Parse(string text)
    {
        var entries = new List<(string, SeamRule, string)>();
        bool header = false;
        string[] lines = text.Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n].TrimEnd('\r');
            if (line.Trim() == "" || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            string[] cells = line.Split('\t').Select(c => c.Trim()).ToArray();
            if (!header)
            {
                header = cells.SequenceEqual(Columns)
                    ? true
                    : throw new FormatException($"line {n + 1}: the header must be the columns {string.Join(", ", Columns)}, tab-separated");
                continue;
            }

            if (cells.Length != Columns.Length || !Enum.TryParse(cells[1], out SeamRule rule) || cells[2] == "")
            {
                throw new FormatException($"line {n + 1}: a seam, one of the rules {string.Join(", ", Enum.GetNames<SeamRule>())}, and a reason");
            }

            entries.Add((cells[0], rule, cells[2]));
        }

        return header ? new SeamExceptions(entries) : throw new FormatException("no header");
    }
}

/// <summary>
/// The rooms held to each other across their exits (docs/gameplay.md#leaving-a-room), pure. The two ends of a way meet at one place
/// on the map (<see cref="ExitMath.RoomOffset"/>); there
/// - the opening is as wide (a hatch) or as tall (a doorway) on both sides (<see cref="SeamRule.Opening"/>);
/// - within <see cref="Around"/> of the opening, everything both rooms' camera bounds show is solid, water or open in both
///   (<see cref="SeamRule.Solids"/>): a hole lands under the matching hole, water never ends at a seam;
/// - water rests on a floor, on other water, or over a hole whose far side is water or floor there (<see cref="SeamRule.Water"/>):
///   no water without a bottom;
/// - a lift that rises through a hatch has a car on the other side too, as wide and in line, at the matching hatch
///   (<see cref="SeamRule.Lift"/>).
/// Solids and water are compared on a grid of <see cref="Step"/>, sampled between the block grid's lines. The room check (an
/// adapter) feeds it the rooms in the game and the ways between them, and prints what it finds less the world's exceptions.
/// </summary>
public sealed class SeamCheck
{
    /// <summary>How far around an opening the rooms are compared, meters: the slab or wall it is cut in and what lies past it.</summary>
    public const float Around = 2f;

    /// <summary>The sampling grid, meters: half the block grid (<see cref="GridSnap.BlockStep"/>), each sample at a cell's center.</summary>
    public const float Step = GridSnap.BlockStep * 0.5f;

    /// <summary>Two sizes closer than this agree, meters.</summary>
    private const float Tolerance = 0.01f;

    private readonly Dictionary<string, SeamRoom> _rooms = new();
    private readonly HashSet<(string Room, string Exit, string To, string ToExit)> _ways = new();

    /// <summary>A room in the game.</summary>
    public void AddRoom(string name, SeamRoom room) => _rooms[name] = room;

    /// <summary>A way from an exit to an exit of another room (both directions of a two-way exit may be added: they are one seam).</summary>
    public void AddWay(string room, string exit, string to, string toExit)
    {
        if (!_ways.Contains((to, toExit, room, exit)))
        {
            _ways.Add((room, exit, to, toExit));
        }
    }

    /// <summary>
    /// What disagrees at the seams, less <paramref name="exceptions"/>, one line each; and each exception nothing breaks any more.
    /// <paramref name="reach"/> is how far above a lift's car its rider reaches (the pilgrim's height), meters.
    /// </summary>
    public IEnumerable<string> Check(SeamExceptions exceptions, float reach)
    {
        var found = Problems(reach).ToList();
        foreach (SeamProblem p in found.Where(p => !exceptions.Entries.Any(e => e.Seam == p.Seam && e.Rule == p.Rule)))
        {
            yield return $"seam {p.Seam} ({p.Rule}): {p.Detail}";
        }

        foreach ((string seam, SeamRule rule, _) in exceptions.Entries.Where(e => !found.Any(p => p.Seam == e.Seam && p.Rule == e.Rule)))
        {
            yield return $"seam {seam} ({rule}) is listed as an exception, but agrees now (or is no seam): delete its line";
        }
    }

    /// <summary>Every disagreement at the seams, the seams in name order.</summary>
    public IEnumerable<SeamProblem> Problems(float reach)
    {
        var problems = new List<SeamProblem>();
        foreach (Seam s in Seams())
        {
            problems.AddRange(Opening(s));
            problems.AddRange(Solids(s));
            problems.AddRange(Lifts(s, reach));
            problems.AddRange(Lifts(s.Flipped(), reach));
        }

        foreach ((string name, SeamRoom room) in _rooms)
        {
            problems.AddRange(Water(name, room));
        }

        return problems.OrderBy(p => p.Seam, StringComparer.Ordinal).ThenBy(p => p.Rule);
    }

    /// <summary>
    /// One seam, seen from one end: the exit <see cref="ExitA"/> (<see cref="At"/>) of <see cref="RoomA"/> and the exit
    /// <see cref="ExitB"/> (<see cref="Other"/>) of <see cref="RoomB"/> it meets; <see cref="Shift"/> takes a position in A's room to B's.
    /// </summary>
    private sealed record Seam(string RoomA, string ExitA, SeamRoom A, Threshold At, string RoomB, string ExitB, SeamRoom B, Threshold Other)
    {
        public Vector2 Shift => Other.Marker - At.Marker;

        /// <summary>The seam's name, its upper or western end first, whichever end it is seen from.</summary>
        public string Name { get; init; } = $"{RoomA}:{ExitA} | {RoomB}:{ExitB}";

        public Seam Flipped() => new(RoomB, ExitB, B, Other, RoomA, ExitA, A, At) { Name = Name };
    }

    private IEnumerable<Seam> Seams()
    {
        foreach ((string room, string exit, string to, string toExit) in _ways)
        {
            if (!_rooms.TryGetValue(room, out SeamRoom? a) || !_rooms.TryGetValue(to, out SeamRoom? b)
                || !a.Exits.TryGetValue(exit, out Threshold at) || !b.Exits.TryGetValue(toExit, out Threshold other))
            {
                continue; // a way to a room or exit not in the game: the room check reports it
            }

            var seam = at.Side is ExitSide.Down or ExitSide.East
                ? new Seam(room, exit, a, at, to, toExit, b, other)
                : new Seam(to, toExit, b, other, room, exit, a, at);
            yield return seam;
        }
    }

    private static IEnumerable<SeamProblem> Opening(Seam s)
    {
        (float a, float b, string what) = s.At.IsHatch ? (s.At.Size.X, s.Other.Size.X, "wide") : (s.At.Size.Y, s.Other.Size.Y, "tall");
        if (MathF.Abs(a - b) > Tolerance)
        {
            yield return new SeamProblem(s.Name, SeamRule.Opening,
                $"the opening is {F(a)} m {what} in {s.RoomA}, {F(b)} m in {s.RoomB}");
        }
    }

    /// <summary>What a room has at a point: inside a solid, else in its water, else open.</summary>
    private enum Fill
    {
        Open,
        Water,
        Solid,
    }

    private static Fill At(SeamRoom room, Vector2 p) =>
        room.Solids.Any(b => Inside(b, p)) ? Fill.Solid : room.Waters.Any(w => Inside(w.Region, p)) ? Fill.Water : Fill.Open;

    private static bool Inside(Box b, Vector2 p) => b.MinX < p.X && p.X < b.MaxX && b.MinY < p.Y && p.Y < b.MaxY;

    /// <summary>The samples of <paramref name="box"/>: the centers of its <see cref="Step"/> cells, on the grid from the origin.</summary>
    private static IEnumerable<Vector2> Samples(Box box)
    {
        for (float x = MathF.Floor(box.MinX / Step) * Step + Step * 0.5f; x < box.MaxX; x += Step)
        {
            for (float y = MathF.Floor(box.MinY / Step) * Step + Step * 0.5f; y < box.MaxY; y += Step)
            {
                if (x > box.MinX && y > box.MinY)
                {
                    yield return new Vector2(x, y);
                }
            }
        }
    }

    private static IEnumerable<SeamProblem> Solids(Seam s)
    {
        Box area = s.At.Area;
        var near = new Box(area.MinX - Around, area.MinY - Around, area.MaxX + Around, area.MaxY + Around);
        Box window = Intersect(Intersect(near, s.A.Bounds), Shifted(s.B.Bounds, -s.Shift));
        if (window.MinX >= window.MaxX || window.MinY >= window.MaxY)
        {
            yield break;
        }

        var differ = new SortedDictionary<(Fill A, Fill B), List<Vector2>>();
        foreach (Vector2 p in Samples(window))
        {
            (Fill a, Fill b) = (At(s.A, p), At(s.B, p + s.Shift));
            if (a != b)
            {
                (differ.TryGetValue((a, b), out List<Vector2>? cells) ? cells : differ[(a, b)] = new List<Vector2>()).Add(p);
            }
        }

        foreach (((Fill a, Fill b), List<Vector2> cells) in differ)
        {
            yield return new SeamProblem(s.Name, SeamRule.Solids,
                $"{Word(a)} in {s.RoomA} but {Word(b)} in {s.RoomB} over {string.Join(", ", Rectangles(cells).Select(D))} ({s.RoomA}'s positions)");
        }
    }

    /// <summary>Sample cells (<see cref="Samples"/>) merged into rectangles: runs along each row, then runs alike on rows in a row.</summary>
    private static IEnumerable<Box> Rectangles(IEnumerable<Vector2> cells)
    {
        static int Index(float v) => (int)MathF.Round(v / Step - 0.5f);
        var open = new Dictionary<(int From, int To), (int Bottom, int Top)>();
        var done = new List<Box>();
        Box Close((int From, int To) run, (int Bottom, int Top) rows) =>
            new(run.From * Step, rows.Bottom * Step, (run.To + 1) * Step, (rows.Top + 1) * Step);
        foreach (IGrouping<int, int> row in cells.GroupBy(c => Index(c.Y), c => Index(c.X)).OrderBy(g => g.Key))
        {
            var runs = new List<(int From, int To)>();
            foreach (int i in row.Order())
            {
                if (runs.Count > 0 && runs[^1].To == i - 1)
                {
                    runs[^1] = (runs[^1].From, i);
                }
                else
                {
                    runs.Add((i, i));
                }
            }

            foreach (var run in open.Keys.Where(r => !runs.Contains(r) || open[r].Top != row.Key - 1).ToList())
            {
                done.Add(Close(run, open[run]));
                open.Remove(run);
            }

            foreach (var run in runs)
            {
                open[run] = open.TryGetValue(run, out var rows) ? (rows.Bottom, row.Key) : (row.Key, row.Key);
            }
        }

        return done.Concat(open.Select(o => Close(o.Key, o.Value))).OrderBy(b => b.MinY).ThenBy(b => b.MinX);
    }

    private static string Word(Fill f) => f switch
    {
        Fill.Solid => "solid",
        Fill.Water => "water",
        _ => "open",
    };

    /// <summary>
    /// Every water of a room rests on something along its bottom: a solid, its other water, or a hole in its floor whose far side is
    /// water or solid there (then the problem is the seam's).
    /// </summary>
    private IEnumerable<SeamProblem> Water(string name, SeamRoom room)
    {
        foreach ((string water, Box region) in room.Waters)
        {
            var bare = new Dictionary<string, List<Vector2>>();
            foreach (Vector2 under in Samples(Intersect(new Box(region.MinX, region.MinY - Step, region.MaxX, region.MinY), room.Bounds)))
            {
                if (At(room, under) != Fill.Open || At(room, under + new Vector2(0f, Step)) != Fill.Water)
                {
                    continue; // held, or its bottom lies hidden in a solid
                }

                string seam = name;
                if (room.Exits.FirstOrDefault(x => x.Value.Side == ExitSide.Down && Inside(x.Value.Trigger, under)) is { Key: { } exit })
                {
                    if (Beyond(name, exit) is not { } beyond)
                    {
                        continue; // a hole to a room not built yet
                    }

                    seam = beyond.Name;
                    if (At(beyond.B, under + beyond.Shift) != Fill.Open)
                    {
                        continue;
                    }
                }

                (bare.TryGetValue(seam, out List<Vector2>? cells) ? cells : bare[seam] = new List<Vector2>()).Add(under);
            }

            foreach ((string seam, List<Vector2> cells) in bare)
            {
                string where = string.Join(", ", Rectangles(cells).Select(D));
                yield return new SeamProblem(seam, SeamRule.Water, seam == name
                    ? $"{name}'s water {water} has no floor under {where}"
                    : $"{name}'s water {water} lies over the hole, but nothing holds it on the far side under {where} ({name}'s positions)");
            }
        }
    }

    /// <summary>The seam beyond exit <paramref name="exit"/> of <paramref name="room"/>, from that room's side, if it leads to one.</summary>
    private Seam? Beyond(string room, string exit) =>
        Seams().Select(s => s.RoomA == room && s.ExitA == exit ? s : s.RoomB == room && s.ExitB == exit ? s.Flipped() : null)
            .FirstOrDefault(s => s is not null);

    /// <summary>A lift of A rising through A's hatch has, in B, a lift as wide and in line that reaches B's hatch.</summary>
    private static IEnumerable<SeamProblem> Lifts(Seam s, float reach)
    {
        if (!s.At.IsHatch)
        {
            yield break;
        }

        foreach (LiftPath lift in s.A.Lifts.Where(l => Swept(l, reach).Overlaps(s.At.Trigger)))
        {
            Box car = Shifted(lift.Platform, s.Shift);
            if (!s.B.Lifts.Any(l => MathF.Abs(l.Platform.MinX - car.MinX) < Tolerance && MathF.Abs(l.Platform.MaxX - car.MaxX) < Tolerance
                                    && Swept(l, reach).Overlaps(s.Other.Trigger)))
            {
                yield return new SeamProblem(s.Name, SeamRule.Lift,
                    $"{s.RoomA}'s lift (its car {F(lift.Platform.MinX)}..{F(lift.Platform.MaxX)}) passes {s.ExitA}, but {s.RoomB} has no car " +
                    $"as wide and in line ({F(car.MinX)}..{F(car.MaxX)} in its positions) at {s.ExitB}");
            }
        }
    }

    /// <summary>Everything a lift's car and its rider pass through, from the lower stop to the upper.</summary>
    private static Box Swept(LiftPath l, float reach) => l.Platform with { MaxY = l.Platform.MaxY + l.Rise + reach };

    private static Box Shifted(Box b, Vector2 by) => new(b.MinX + by.X, b.MinY + by.Y, b.MaxX + by.X, b.MaxY + by.Y);

    private static Box Intersect(Box a, Box b) =>
        new(MathF.Max(a.MinX, b.MinX), MathF.Max(a.MinY, b.MinY), MathF.Min(a.MaxX, b.MaxX), MathF.Min(a.MaxY, b.MaxY));

    private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string D(Box b) => $"({F(b.MinX)}, {F(b.MinY)})..({F(b.MaxX)}, {F(b.MaxY)})";
}
