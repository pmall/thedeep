using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>A way from one room to another: leaving <see cref="From"/> through its exit, needing <see cref="Requires"/>.</summary>
public sealed record MapEdge(string From, string FromExit, string To, string ToExit, Requirement Requires);

/// <summary>An ability relic lying in a room (taken on entering it).</summary>
public sealed record MapRelic(string Room, string Name, Ability Ability);

/// <summary>An item lying in a room (a key, a code; taken on entering it, held for good), by its id.</summary>
public sealed record MapItem(string Room, string Name, string Item);

/// <summary>
/// The world as the solver sees it, pure: rooms, the exits between them (one edge per exit, for leaving
/// through it), the ability relics in them, where the pilgrim starts and where the journey ends. Built by
/// the extractor from the room scenes' markers (never by hand), or by tests. Items (keys, codes) lie in rooms like relics and gate
/// exits like abilities.
/// </summary>
public sealed class MapGraph
{
    private readonly List<MapEdge> _edges = new();
    private readonly List<MapRelic> _relics = new();
    private readonly List<MapItem> _items = new();
    private readonly SortedSet<string> _rooms = new(System.StringComparer.Ordinal);
    private readonly SortedSet<string> _goals = new(System.StringComparer.Ordinal);

    /// <summary>The room where the pilgrim starts; empty until set.</summary>
    public string Start { get; set; } = "";

    /// <summary>Every room's name, sorted: those added and those an edge, a relic, an item or a goal names.</summary>
    public IReadOnlyCollection<string> Rooms => _rooms;
    /// <summary>Every way out of a room, one per exit, in the order added.</summary>
    public IReadOnlyList<MapEdge> Edges => _edges;
    /// <summary>Every ability relic lying in a room, in the order added.</summary>
    public IReadOnlyList<MapRelic> Relics => _relics;
    /// <summary>Every item (a key, a code) lying in a room, in the order added.</summary>
    public IReadOnlyList<MapItem> Items => _items;
    /// <summary>The rooms where the journey ends, sorted.</summary>
    public IReadOnlyCollection<string> Goals => _goals;

    /// <summary>Adds a room by name; adding it again changes nothing.</summary>
    public void AddRoom(string room) => _rooms.Add(room);

    /// <summary>Adds a way out of a room, and both its rooms if they are new.</summary>
    public void AddEdge(MapEdge edge)
    {
        _rooms.Add(edge.From);
        _rooms.Add(edge.To);
        _edges.Add(edge);
    }

    /// <summary>Lays an ability relic in its room, adding the room if it is new.</summary>
    public void AddRelic(MapRelic relic)
    {
        _rooms.Add(relic.Room);
        _relics.Add(relic);
    }

    /// <summary>Lays an item in its room, adding the room if it is new.</summary>
    public void AddItem(MapItem item)
    {
        _rooms.Add(item.Room);
        _items.Add(item);
    }

    /// <summary>Makes <paramref name="room"/> a place where the journey ends, adding the room if it is new.</summary>
    public void AddGoal(string room)
    {
        _rooms.Add(room);
        _goals.Add(room);
    }

    /// <summary>The ways out of <paramref name="room"/>.</summary>
    public IEnumerable<MapEdge> From(string room) => _edges.Where(e => e.From == room);

    /// <summary>The ability relics lying in <paramref name="room"/>.</summary>
    public IEnumerable<MapRelic> RelicsIn(string room) => _relics.Where(i => i.Room == room);

    /// <summary>The items lying in <paramref name="room"/>.</summary>
    public IEnumerable<MapItem> ItemsIn(string room) => _items.Where(i => i.Room == room);

    /// <summary>
    /// Items against the ways they open: every item an exit's requirement names lies in exactly one room (a misspelled ability is an
    /// item nothing gives, caught here), and every item lying in a room opens some way, here or among <paramref name="elsewhere"/>
    /// (the requirements of ways to rooms not in the graph yet): a key to nothing is a mistake.
    /// </summary>
    public IEnumerable<string> ItemProblems(IEnumerable<Requirement> elsewhere)
    {
        var named = new SortedDictionary<string, MapEdge>(System.StringComparer.Ordinal);
        foreach (MapEdge edge in _edges)
        {
            foreach (string item in edge.Requires.Items.Ids)
            {
                named.TryAdd(item, edge);
            }
        }

        foreach ((string item, MapEdge edge) in named)
        {
            int lying = _items.Count(i => i.Item == item);
            if (lying != 1)
            {
                yield return $"{edge.From}:{edge.FromExit} needs {item}, which lies in {lying} rooms (an item lies in one; the abilities are " +
                             $"{string.Join(", ", AbilitySet.All)})";
            }
        }

        var opening = new HashSet<string>(named.Keys.Concat(elsewhere.SelectMany(r => r.Items.Ids)));
        foreach (MapItem item in _items.Where(i => !opening.Contains(i.Item)))
        {
            yield return $"the {item.Item} in {item.Room} ({item.Name}) opens no way (no exit's requirement names it)";
        }
    }

    /// <summary>Every ability some relic gives.</summary>
    public Ability AbilitiesOnMap => _relics.Aggregate(Ability.None, (all, i) => all | i.Ability);
}
