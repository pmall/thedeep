using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A room's own ground routes as the reach planner flies them, chosen to be played in the real game with the real inputs (the route
/// check, <c>RouteCheck</c>), so the planner's model of the moves and the real moves can't drift apart unseen. From every way into the
/// room (its exits' arrivals, its wakes, its spawn point) the planner with no ability finds the floors it stands on; its moves from those
/// (<see cref="Leap"/>) are the routes. Only flights the real body can follow move for move are played: clean ones (no wall,
/// ceiling, ledge assist, water or hazard on the way), clear of every solid by <see cref="Clearance"/> at the sides and overhead, kept
/// <see cref="KeepOff"/> from what would take the pilgrim elsewhere (exits, relics, items, goals, water, heat, slag, lifts), landing with
/// <see cref="Footing"/> of floor either side, and with room on the take-off floor for a run-up. Of those, <see cref="Pick"/> takes
/// the longest flight of each kind of move in turn, each between a pair of floors no other route joins. <see cref="Mismatch"/> judges
/// the real flight against the planned one.
/// </summary>
public static class GroundRoutes
{
    /// <summary>How far a played flight keeps from every solid beside and over the body, meters: the real body may stray that far.</summary>
    public const float Clearance = 0.15f;

    /// <summary>How far a played flight's body keeps from triggers, water, heat, slag and lifts, meters.</summary>
    public const float KeepOff = 0.5f;

    /// <summary>Floor either side of a landing's feet, meters: a capsule landing nearer an edge rolls off it.</summary>
    public const float Footing = 0.5f;

    /// <summary>Floor either side of a standing take-off's feet, meters.</summary>
    public const float Stance = 0.2f;

    /// <summary>Ticks the real pilgrim runs on the take-off floor before a running take-off, so it leaves at speed and on its feet.</summary>
    public const int RunUpTicks = 3;

    /// <summary>The fewest ticks a played route lasts, from the take-off to the landing.</summary>
    public const int MinTicks = 12;

    /// <summary>
    /// The farthest the real path of a jump may stray from the planned one, meters: its farthest point from the planned path's line, or
    /// the planned path's from its. The two agree to a few millimeters; a jump 2% higher peaks 0.07 m higher.
    /// </summary>
    public const float PathTolerance = 0.05f;

    /// <summary>
    /// The same for a walk-off, its path and its landing, meters. The planner's body is a box, which stands on an edge until all of it is
    /// past; the pilgrim's round foot rolls off a quarter of a meter sooner: it lands that much short, and the ticks it falls earlier show
    /// where an air dash then runs level (0.25 m seen).
    /// </summary>
    public const float WalkOffTolerance = 0.4f;

    /// <summary>The farthest the real landing may be from the planned one along the floor, meters: a tick of a run, and a little.</summary>
    public const float LandingTolerance = 0.15f;

    private const float Dt = 1f / 60f;

    /// <summary>The kinds of move, in the order routes are picked: those whose drift shows most first.</summary>
    private static readonly (int Jump, bool Dash)[] Kinds =
    {
        (-1, true), (0, true), (-1, false), (12, false), (0, false), (6, true), (12, true), (6, false),
    };

    /// <summary>
    /// Up to <paramref name="count"/> ground routes of the room of <paramref name="shape"/>, its moves simulated with
    /// <paramref name="limits"/>; <paramref name="keepOff"/> adds boxes the routes keep off (solids the planner doesn't see, such as a
    /// gate or a fence that stands until an event). The kinds take turns from the <paramref name="turn"/>th (rooms given turns in a row
    /// share the kinds out between them).
    /// </summary>
    public static List<GroundRoute> Pick(RoomShape shape, PlannerLimits limits, int count, IEnumerable<Box>? keepOff = null, int turn = 0)
    {
        ReachPlanner planner = WorldCheck.For(shape, limits, Ability.None);
        IEnumerable<(Vector2 At, Vector2 Velocity, int Steer)> ways = shape.Exits.Where(x => !x.Sealed).Select(x => x.Entry(limits, shape.Solids))
            .Concat(shape.Wakes.Append(shape.Spawn).Select(w => (w, Vector2.Zero, 0)));
        var leaps = new List<Leap>();
        planner.Reach(ways, new List<Box>(), leaps.Add);

        float liftClear = limits.Move.Height + limits.Rise;
        List<Box> avoid = shape.Exits.Select(x => x.Threshold.Trigger)
            .Concat(shape.Relics.Select(r => r.Area)).Concat(shape.Items.Select(i => i.Area)).Concat(shape.Goals)
            .Concat(shape.Waters).Concat(shape.Heat).Concat(shape.Slag)
            .Concat(shape.Lifts.Select(l => l.Platform with { MaxY = l.Platform.MaxY + l.Rise + liftClear }))
            .Concat(keepOff ?? Enumerable.Empty<Box>()).ToList();
        var routes = leaps.Select(leap => Playable(leap, planner.Ledges, shape.Solids, avoid, limits.Move)).OfType<GroundRoute>().ToList();

        // The longest flight of each kind in turn, each joining a pair of floors (or one floor to itself) no route picked yet joins.
        var byKind = Enumerable.Range(0, Kinds.Length).Select(k => Kinds[(k + turn) % Kinds.Length])
            .Select(k => new Queue<GroundRoute>(routes.Where(r => Kind(r.Leap.Move) == k).OrderByDescending(r => Length(r.Leap.Path)))).ToList();
        var joined = new HashSet<(int, int)>();
        var picked = new List<GroundRoute>();
        bool more = true;
        while (picked.Count < count && more)
        {
            more = false;
            foreach (Queue<GroundRoute> queue in byKind)
            {
                while (queue.TryDequeue(out GroundRoute? route))
                {
                    if (joined.Add((route.Leap.From, route.Leap.Lands)))
                    {
                        picked.Add(route);
                        more = true;
                        break;
                    }
                }

                if (picked.Count == count)
                {
                    break;
                }
            }
        }

        return picked;
    }

    /// <summary>
    /// What is wrong with a real flight of <paramref name="route"/>: <paramref name="real"/> are its feet tick by tick from the take-off,
    /// <paramref name="landed"/> whether it ended standing on a floor. Null when it landed where the planner said, along its path.
    /// </summary>
    public static string? Mismatch(GroundRoute route, IReadOnlyList<Vector2> real, bool landed)
    {
        IReadOnlyList<Vector2> plan = route.Leap.Path;
        Vector2 end = real[^1], planned = plan[^1];
        if (!landed)
        {
            return string.Create(CultureInfo.InvariantCulture, $"never landed: at ({end.X:0.##}, {end.Y:0.##}) after {real.Count} ticks, " +
                                                               $"planned on the floor at {planned.Y:0.##} by tick {plan.Count}");
        }

        bool walkOff = route.Leap.Move.JumpTicks == 0;
        if (MathF.Abs(end.Y - planned.Y) > 0.05f || MathF.Abs(end.X - planned.X) > (walkOff ? WalkOffTolerance : LandingTolerance))
        {
            return string.Create(CultureInfo.InvariantCulture, $"landed at ({end.X:0.##}, {end.Y:0.##}) after {real.Count} ticks, " +
                                                               $"planned at ({planned.X:0.##}, {planned.Y:0.##}) after {plan.Count}");
        }

        (float away, Vector2 at) = Farthest(real, plan);
        (float back, Vector2 missed) = Farthest(plan, real);
        if (MathF.Max(away, back) > (walkOff ? WalkOffTolerance : PathTolerance))
        {
            return away >= back
                ? string.Create(CultureInfo.InvariantCulture, $"strayed {away:0.##} m from the planned path, at ({at.X:0.##}, {at.Y:0.##})")
                : string.Create(CultureInfo.InvariantCulture, $"passed {back:0.##} m from the planned path's ({missed.X:0.##}, {missed.Y:0.##})");
        }

        return null;
    }

    /// <summary>The farthest of the points of <paramref name="path"/> from the line of <paramref name="from"/>, and that point.</summary>
    public static (float Distance, Vector2 At) Farthest(IReadOnlyList<Vector2> path, IReadOnlyList<Vector2> from)
    {
        (float worst, Vector2 at) = (0f, path[0]);
        foreach (Vector2 p in path)
        {
            float d = float.PositiveInfinity;
            for (int i = 0; i < from.Count; i++)
            {
                d = MathF.Min(d, ToSegment(p, from[i], from[Math.Min(i + 1, from.Count - 1)]));
            }

            if (d > worst)
            {
                (worst, at) = (d, p);
            }
        }

        return (worst, at);
    }

    private static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.LengthSquared() < 1e-12f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }

    private static (int, bool) Kind(GroundMove m) => (m.JumpTicks, m.DashTick >= 0);

    private static float Length(IReadOnlyList<Vector2> path)
    {
        float length = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            length += Vector2.Distance(path[i - 1], path[i]);
        }

        return length;
    }

    /// <summary>The leap as a route the real pilgrim can follow move for move, or null (see <see cref="GroundRoutes"/>).</summary>
    private static GroundRoute? Playable(Leap leap, IReadOnlyList<Ledge> ledges, IReadOnlyList<Box> solids, IReadOnlyList<Box> avoid, MoveLimits m)
    {
        if (!leap.Clean || leap.Path.Count < MinTicks || ledges[leap.Lands] is not { Swim: false } landing)
        {
            return null;
        }

        Vector2 takeOff = leap.TakeOff, end = leap.Path[^1];
        int dir = leap.Move.Dir;
        var start = new Vector2(takeOff.X - dir * m.RunSpeed * Dt * RunUpTicks, takeOff.Y);
        bool footing = dir == 0 ? Supported(ledges, takeOff.Y, takeOff.X - Stance, takeOff.X + Stance)
            : Supported(ledges, takeOff.Y, MathF.Min(start.X, takeOff.X), MathF.Max(start.X, takeOff.X));
        if (!footing || !Supported(ledges, landing.Y, end.X - Footing, end.X + Footing))
        {
            return null;
        }

        // The clearance is the body's above its round foot (a capsule's, as wide as the body): what passes under that, the foot rolls
        // over, so a walk-off falling past the edge it left is clean.
        float half = m.Width * 0.5f;
        foreach (Vector2 p in leap.Path.Append(start))
        {
            var clear = new Box(p.X - half - Clearance, p.Y + half, p.X + half + Clearance, p.Y + m.Height + Clearance);
            var body = new Box(p.X - half, p.Y, p.X + half, p.Y + m.Height).Grown(KeepOff);
            foreach (Box s in solids)
            {
                if (s.Overlaps(clear))
                {
                    return null;
                }
            }

            foreach (Box a in avoid)
            {
                if (a.Overlaps(body))
                {
                    return null;
                }
            }
        }

        return new GroundRoute(leap, start, landing);
    }

    /// <summary>Whether floor (a ledge's standing range at <paramref name="y"/>) lies under every point from <paramref name="a"/> to <paramref name="b"/>.</summary>
    private static bool Supported(IReadOnlyList<Ledge> ledges, float y, float a, float b)
    {
        for (float x = a; ; x = MathF.Min(x + 0.05f, b))
        {
            if (!ledges.Any(l => !l.Swim && MathF.Abs(l.Y - y) < 0.01f && x >= l.MinX - 0.001f && x <= l.MaxX + 0.001f))
            {
                return false;
            }

            if (x >= b)
            {
                return true;
            }
        }
    }
}

/// <summary>
/// A ground route to play (<see cref="GroundRoutes"/>): the planner's <see cref="Leap"/>, where the real pilgrim starts
/// (<see cref="Start"/>: a run-up behind a running take-off, the take-off itself for a standing one) and the floor it must land on.
/// </summary>
public sealed record GroundRoute(Leap Leap, Vector2 Start, Ledge Landing)
{
    /// <summary>The route in words, for a problem line.</summary>
    public string What
    {
        get
        {
            GroundMove m = Leap.Move;
            string move = m.JumpTicks switch
            {
                0 => "walking off",
                < 0 => "a full jump",
                _ => $"a jump cut after {m.JumpTicks} ticks",
            };
            string way = m.Dir switch { < 0 => " west", > 0 => " east", _ => " straight up" };
            string dash = m.DashTick >= 0 ? $", an air dash at tick {m.DashTick}" : "";
            Vector2 end = Leap.Path[^1];
            return string.Create(CultureInfo.InvariantCulture,
                $"{move}{way}{dash} from ({Leap.TakeOff.X:0.##}, {Leap.TakeOff.Y:0.##}) onto ({end.X:0.##}, {end.Y:0.##})");
        }
    }
}
