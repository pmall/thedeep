using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TheDeep;

/// <summary>
/// What a room is for in the plan (docs/roadmap.md#stage-plan): the closed set a room table's <c>kinds</c> column draws from. Some
/// kinds are facts the network states too, and <see cref="RoomTable.Check"/> holds them to it: <see cref="Start"/>, <see cref="Goal"/>,
/// <see cref="Relic"/>, <see cref="Boss"/>, <see cref="Ambush"/>, <see cref="PowerUp"/>, <see cref="Milestone"/> (iff the network says so) and
/// <see cref="Secret"/> (every way in hidden). The others are design intent the network can't see.
/// </summary>
public enum RoomKind
{
    /// <summary>Where a new game wakes (the network's <c>start</c>).</summary>
    Start,

    /// <summary>Where the journey ends (a <c>goal</c>).</summary>
    Goal,

    /// <summary>A crossroads several ways meet at.</summary>
    Hub,

    /// <summary>A way between places, little else.</summary>
    Corridor,

    /// <summary>An ability lies here (a <c>relic</c>).</summary>
    Relic,

    /// <summary>A boss is fought here (a <c>boss</c>).</summary>
    Boss,

    /// <summary>A story beat tied to depth happens here (a <c>milestone</c>: a law break).</summary>
    Milestone,

    /// <summary>A shrine to rest and save at.</summary>
    Shrine,

    /// <summary>A room off the map's open ways: every way in is <c>hidden</c>.</summary>
    Secret,

    /// <summary>An enemy trap: the room seals while a wave stands (an <c>ambush</c>).</summary>
    Ambush,

    /// <summary>A power-up lies here (a <c>powerup</c>: strength or defense, for good).</summary>
    PowerUp,

    /// <summary>A puzzle: a switch and its mechanism, a code, a door to work out.</summary>
    Enigma,

    /// <summary>Scripture, a conversation, something to learn.</summary>
    Lore,
}

/// <summary>One row of a <see cref="RoomTable"/>.</summary>
public sealed record RoomRow(string Room, string Layer, IReadOnlyList<RoomKind> Kinds, string Happens, IReadOnlyList<string> Machines,
    IReadOnlyList<string> Items, string Notes);

/// <summary>
/// A world's room table (docs/roadmap.md#stage-plan), pure: one row per room of its network, read from a tab-separated file beside
/// it. The first line that isn't a comment ('#') is the header, exactly these columns:
/// <code>
/// room	layer	kinds	happens	machines	items	notes
/// Narthex	Flooded	start, hub, lore	the pilgrim wakes in the porch	Lamplighter		the first room
/// </code>
/// <c>layer</c> is the network's layer of the room; <c>kinds</c> (at least one), <c>machines</c> and <c>items</c> are lists separated
/// by commas, <c>kinds</c> drawn from <see cref="RoomKind"/> (any case); <c>happens</c> and <c>notes</c> are free text. Trailing
/// empty columns may be left out.
/// </summary>
public sealed class RoomTable
{
    /// <summary>The header, in order.</summary>
    public static readonly string[] Columns = { "room", "layer", "kinds", "happens", "machines", "items", "notes" };

    private readonly List<RoomRow> _rows = new();

    public IReadOnlyList<RoomRow> Rows => _rows;

    public RoomRow? Row(string room) => _rows.FirstOrDefault(r => r.Room == room);

    /// <summary>Parses a table; errors name the file's line.</summary>
    public static RoomTable Parse(string text)
    {
        var table = new RoomTable();
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
                if (!cells.SequenceEqual(Columns))
                {
                    throw new FormatException($"line {n + 1}: the header must be the columns {string.Join(", ", Columns)}, tab-separated");
                }

                header = true;
                continue;
            }

            try
            {
                table._rows.Add(table.Row(cells));
            }
            catch (FormatException e)
            {
                throw new FormatException($"line {n + 1}: {e.Message}");
            }
        }

        if (!header)
        {
            throw new FormatException("no header");
        }

        return table;
    }

    private RoomRow Row(string[] cells)
    {
        if (cells.Length > Columns.Length)
        {
            throw new FormatException($"{cells.Length} columns, {Columns.Length} at most (a tab inside a cell?)");
        }

        string Cell(int i) => i < cells.Length ? cells[i] : "";
        static List<string> List(string cell) => cell.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        string room = Cell(0), layer = Cell(1);
        if (room == "" || room.Contains(' ') || layer == "" || layer.Contains(' '))
        {
            throw new FormatException("a row starts with a room and its layer, one word each");
        }

        if (Row(room) is not null)
        {
            throw new FormatException($"room {room} has two rows");
        }

        var kinds = new List<RoomKind>();
        foreach (string word in List(Cell(2)))
        {
            if (!Enum.TryParse(word, ignoreCase: true, out RoomKind kind) || !Enum.IsDefined(kind) || int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                throw new FormatException($"unknown kind {word} (the kinds: {string.Join(", ", Enum.GetNames<RoomKind>().Select(k => k.ToLowerInvariant()))})");
            }

            kinds.Add(kind);
        }

        if (kinds.Count == 0)
        {
            throw new FormatException($"{room} has no kind");
        }

        return new RoomRow(room, layer, kinds, Cell(3), List(Cell(4)), List(Cell(5)), Cell(6));
    }

    /// <summary>
    /// Holds the table and the network to each other: the same rooms on the same layers; a start, goal, relic, boss, ambush,
    /// power-up or milestone room where the network has one, and only there; a secret room entered only by hidden ways (at least one), and every
    /// hidden way into or out of a secret room; each room's items the network's items there. Returns the problems.
    /// </summary>
    public List<string> Check(MapDraft network)
    {
        var problems = new List<string>();
        foreach (string room in network.Graph.Rooms.Where(r => Row(r) is null))
        {
            problems.Add($"{room} is in the network but has no row");
        }

        foreach (RoomRow row in _rows)
        {
            if (!network.Layers.TryGetValue(row.Room, out string? layer))
            {
                problems.Add($"{row.Room} has a row but is not in the network");
                continue;
            }

            if (layer != row.Layer)
            {
                problems.Add($"{row.Room}: the table's layer is {row.Layer}, the network's {layer}");
            }

            void Fact(RoomKind kind, bool there, string what)
            {
                bool said = row.Kinds.Contains(kind);
                if (there && !said)
                {
                    problems.Add($"{row.Room}: the network has {what} there, the table's kinds don't say {kind.ToString().ToLowerInvariant()}");
                }
                else if (said && !there)
                {
                    problems.Add($"{row.Room}: the table says {kind.ToString().ToLowerInvariant()}, but the network has none there");
                }
            }

            string r = row.Room;
            Fact(RoomKind.Start, network.Graph.Start == r, "the start");
            Fact(RoomKind.Goal, network.Graph.Goals.Contains(r), "a goal");
            Fact(RoomKind.Relic, network.Graph.RelicsIn(r).Any(), "a relic");
            Fact(RoomKind.Boss, network.Bosses.Any(b => b.Room == r), "a boss");
            Fact(RoomKind.Ambush, network.Ambushes.Contains(r), "an ambush");
            Fact(RoomKind.PowerUp, network.PowerUps.Any(p => p.Room == r), "a power-up");
            Fact(RoomKind.Milestone, network.Milestones.Any(m => m.Room == r), "a milestone");

            if (row.Kinds.Contains(RoomKind.Secret))
            {
                var ways = network.Graph.Edges.Where(e => e.To == r).ToList();
                if (ways.Count == 0)
                {
                    problems.Add($"{r}: a secret room, but no way leads into it");
                }

                foreach (MapEdge way in ways.Where(e => !network.IsHidden(e.From, e.To)))
                {
                    problems.Add($"{r}: a secret room, but the way from {way.From} is not hidden (the network's 'hidden {way.From} {r}')");
                }
            }

            var items = network.Graph.ItemsIn(r).Select(i => i.Item).Order(StringComparer.Ordinal).ToList();
            var listed = row.Items.Order(StringComparer.Ordinal).ToList();
            if (!items.SequenceEqual(listed))
            {
                problems.Add($"{r}: the table's items are [{string.Join(", ", listed)}], the network places [{string.Join(", ", items)}] there");
            }
        }

        foreach ((string from, string to) in network.Hidden)
        {
            if (Row(from)?.Kinds.Contains(RoomKind.Secret) != true && Row(to)?.Kinds.Contains(RoomKind.Secret) != true)
            {
                problems.Add($"the hidden way between {from} and {to} leads into and out of no secret room");
            }
        }

        return problems;
    }
}
