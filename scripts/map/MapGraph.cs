using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>A way from one room to another: leaving <see cref="From"/> through its exit, needing <see cref="Requires"/>.</summary>
public sealed record MapEdge(string From, string FromExit, string To, string ToExit, Requirement Requires);

/// <summary>An ability relic lying in a room (taken on entering it).</summary>
public sealed record MapItem(string Room, string Name, Ability Ability);

/// <summary>
/// The world as the solver sees it, pure: rooms, the exits between them (one edge per exit, for leaving
/// through it), the ability relics in them, where the pilgrim starts and where the journey ends. Built by
/// the extractor from the room scenes' markers (never by hand), or by tests.
/// </summary>
public sealed class MapGraph
{
    private readonly List<MapEdge> _edges = new();
    private readonly List<MapItem> _items = new();
    private readonly SortedSet<string> _rooms = new(System.StringComparer.Ordinal);
    private readonly SortedSet<string> _goals = new(System.StringComparer.Ordinal);

    public string Start { get; set; } = "";

    public IReadOnlyCollection<string> Rooms => _rooms;
    public IReadOnlyList<MapEdge> Edges => _edges;
    public IReadOnlyList<MapItem> Items => _items;
    public IReadOnlyCollection<string> Goals => _goals;

    public void AddRoom(string room) => _rooms.Add(room);

    public void AddEdge(MapEdge edge)
    {
        _rooms.Add(edge.From);
        _rooms.Add(edge.To);
        _edges.Add(edge);
    }

    public void AddItem(MapItem item)
    {
        _rooms.Add(item.Room);
        _items.Add(item);
    }

    public void AddGoal(string room)
    {
        _rooms.Add(room);
        _goals.Add(room);
    }

    public IEnumerable<MapEdge> From(string room) => _edges.Where(e => e.From == room);

    public IEnumerable<MapItem> In(string room) => _items.Where(i => i.Room == room);

    /// <summary>Every ability some relic gives.</summary>
    public Ability AbilitiesOnMap => _items.Aggregate(Ability.None, (all, i) => all | i.Ability);
}
