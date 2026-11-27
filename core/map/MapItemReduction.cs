using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// Exact item-state reductions for <see cref="MapSolver.Solve"/>: identifies items that never need a bit in
/// <see cref="SolverState.Held"/> during the main search, preventing 2^items state blowup on large maps with many keys,
/// codes and levers while keeping reachability, softlock detection and ability orders exact.
/// <list type="bullet">
/// <item><b>Local items</b>: placed in a relic-free room sharing a requirement-free, relic-free two-way component with every
/// edge origin that checks the item. Taking the item and returning never changes owned abilities or crosses a one-way edge.</item>
/// <item><b>Optional pockets</b>: dead-end branches disconnected from start when the item's doors are removed, holding no goal,
/// relic or item, with requirement-free paths back to an exitable entry door. Verified after the main search.</item>
/// </list>
/// </summary>
internal sealed class MapItemReduction
{
    /// <summary>A dead-end pocket behind one item's locked doors: its rooms and the entry doors from outside.</summary>
    public sealed record OptionalPocket(HashSet<string> Rooms, List<MapEdge> Entries);

    private readonly Dictionary<string, OptionalPocket> _optionalPockets;
    private readonly HashSet<string> _ignored;

    private MapItemReduction(ItemSet localItems, Dictionary<string, OptionalPocket> optionalPockets)
    {
        LocalItems = localItems;
        _optionalPockets = optionalPockets;
        _ignored = new HashSet<string>(localItems.Ids, StringComparer.Ordinal);
        foreach (string item in optionalPockets.Keys)
        {
            _ignored.Add(item);
        }
    }

    /// <summary>Items always freely obtainable at every door that checks them.</summary>
    public ItemSet LocalItems { get; }

    /// <summary>Isolated optional pockets factored out of the main search, keyed by their gating item.</summary>
    public IReadOnlyDictionary<string, OptionalPocket> OptionalPockets => _optionalPockets;

    /// <summary>Whether <paramref name="item"/> is factored out of <see cref="SolverState.Held"/>.</summary>
    public bool Ignored(string item) => _ignored.Contains(item);

    /// <summary>Whether <paramref name="requires"/> mentions an item whose pocket is checked separately.</summary>
    public bool MentionsOptional(Requirement requires)
    {
        if (_optionalPockets.Count == 0 || requires.Items.Count == 0)
        {
            return false;
        }

        foreach (string id in requires.Items.Ids)
        {
            if (_optionalPockets.ContainsKey(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Computes the exact local and optional-pocket reductions for <paramref name="map"/>.</summary>
    public static MapItemReduction Analyze(MapGraph map)
    {
        ItemSet local = FindLocalItems(map);
        return new MapItemReduction(local, FindOptionalPockets(map, local));
    }

    /// <summary>Whether some reachable state visits a placement of <paramref name="item"/> and can subsequently open an entry door of <paramref name="pocket"/>.</summary>
    public bool PocketReachable(MapGraph map, string item, OptionalPocket pocket, IEnumerable<SolverState> reachable,
        Dictionary<SolverState, List<SolverState>> successors)
    {
        var placementRooms = map.Items.Where(i => i.Item == item).Select(i => i.Room).ToHashSet(StringComparer.Ordinal);
        var entriesByRoom = new Dictionary<string, List<MapEdge>>(StringComparer.Ordinal);
        foreach (MapEdge e in pocket.Entries)
        {
            if (!entriesByRoom.TryGetValue(e.From, out List<MapEdge>? list))
            {
                entriesByRoom[e.From] = list = new List<MapEdge>();
            }

            list.Add(e);
        }

        ItemSet itemAndLocal = LocalItems.With(item);
        var seen = new HashSet<SolverState>();
        var queue = new Queue<SolverState>();
        foreach (SolverState s in reachable)
        {
            if (placementRooms.Contains(s.Room) && seen.Add(s))
            {
                queue.Enqueue(s);
            }
        }

        while (queue.Count > 0)
        {
            SolverState s = queue.Dequeue();
            if (entriesByRoom.TryGetValue(s.Room, out List<MapEdge>? doors))
            {
                ItemSet held = s.Held.Union(itemAndLocal);
                if (doors.Any(d => d.Requires.IsMetBy(s.Owned, held)))
                {
                    return true;
                }
            }

            foreach (SolverState next in successors[s])
            {
                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    private static bool IsFree(Requirement r) => r.IsMetBy(Ability.None, ItemSet.Empty);

    private static ItemSet FindLocalItems(MapGraph map)
    {
        if (map.Items.Count == 0)
        {
            return ItemSet.Empty;
        }

        var freeEdges = new HashSet<(string, string)>();
        foreach (MapEdge e in map.Edges)
        {
            if (IsFree(e.Requires))
            {
                freeEdges.Add((e.From, e.To));
            }
        }

        var comp = new Dictionary<string, int>(StringComparer.Ordinal);
        int nextComp = 0;
        var queue = new Queue<string>();
        foreach (string room in map.Rooms)
        {
            if (comp.ContainsKey(room) || map.RelicsIn(room).Any())
            {
                continue;
            }

            int id = nextComp++;
            comp[room] = id;
            queue.Enqueue(room);
            while (queue.Count > 0)
            {
                string u = queue.Dequeue();
                foreach (MapEdge e in map.From(u))
                {
                    if (IsFree(e.Requires) && freeEdges.Contains((e.To, u)) && !map.RelicsIn(e.To).Any() && !comp.ContainsKey(e.To))
                    {
                        comp[e.To] = id;
                        queue.Enqueue(e.To);
                    }
                }
            }
        }

        var local = new List<string>();
        foreach (string item in map.Items.Select(i => i.Item).Distinct(StringComparer.Ordinal))
        {
            var placed = map.Items.Where(i => i.Item == item).Select(i => i.Room).ToHashSet(StringComparer.Ordinal);
            var placedComps = new HashSet<int>();
            foreach (string r in placed)
            {
                if (comp.TryGetValue(r, out int c))
                {
                    placedComps.Add(c);
                }
            }

            bool allLocal = true;
            foreach (MapEdge e in map.Edges)
            {
                if (!e.Requires.Items.Contains(item))
                {
                    continue;
                }

                if (!placed.Contains(e.From) && (!comp.TryGetValue(e.From, out int c) || !placedComps.Contains(c)))
                {
                    allLocal = false;
                    break;
                }
            }

            if (allLocal)
            {
                local.Add(item);
            }
        }

        return local.Count == 0 ? ItemSet.Empty : ItemSet.Of(local);
    }

    private static Dictionary<string, OptionalPocket> FindOptionalPockets(MapGraph map, ItemSet localItems)
    {
        var result = new Dictionary<string, OptionalPocket>(StringComparer.Ordinal);
        if (map.Items.Count == 0)
        {
            return result;
        }

        foreach (string item in map.Items.Select(i => i.Item).Distinct(StringComparer.Ordinal))
        {
            if (localItems.Contains(item))
            {
                continue;
            }

            var itemEdges = map.Edges.Where(e => e.Requires.Items.Contains(item)).ToList();
            if (itemEdges.Count == 0)
            {
                continue;
            }

            var undirected = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string r in map.Rooms)
            {
                undirected[r] = new List<string>();
            }

            foreach (MapEdge e in map.Edges)
            {
                if (e.Requires.Items.Contains(item))
                {
                    continue;
                }

                undirected[e.From].Add(e.To);
                undirected[e.To].Add(e.From);
            }

            var main = new HashSet<string>(StringComparer.Ordinal);
            var q = new Queue<string>();
            if (undirected.ContainsKey(map.Start))
            {
                main.Add(map.Start);
                q.Enqueue(map.Start);
            }

            while (q.Count > 0)
            {
                string u = q.Dequeue();
                foreach (string v in undirected[u])
                {
                    if (main.Add(v))
                    {
                        q.Enqueue(v);
                    }
                }
            }

            var pocketRooms = new HashSet<string>(map.Rooms.Where(r => !main.Contains(r)), StringComparer.Ordinal);
            if (pocketRooms.Count == 0 || itemEdges.Any(e => main.Contains(e.From) == main.Contains(e.To)))
            {
                continue;
            }

            if (pocketRooms.Any(r => map.Goals.Contains(r) || map.RelicsIn(r).Any() || map.ItemsIn(r).Any()))
            {
                continue;
            }

            var entries = itemEdges.Where(e => main.Contains(e.From) && pocketRooms.Contains(e.To)).ToList();
            if (entries.Count == 0)
            {
                continue;
            }

            bool allReturnable = entries.All(entry =>
                map.From(entry.To).Any(back => back.To == entry.From
                    && entry.Requires.Options.All(opt => back.Requires.IsMetBy(opt.Abilities, opt.Items))));
            if (!allReturnable || !PocketInternallyConnected(map, pocketRooms, entries))
            {
                continue;
            }

            result[item] = new OptionalPocket(pocketRooms, entries);
        }

        return result;
    }

    private static bool PocketInternallyConnected(MapGraph map, HashSet<string> pocketRooms, List<MapEdge> entries)
    {
        var freeRev = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string r in pocketRooms)
        {
            freeRev[r] = new List<string>();
        }

        foreach (string r in pocketRooms)
        {
            foreach (MapEdge e in map.From(r))
            {
                if (pocketRooms.Contains(e.To) && IsFree(e.Requires))
                {
                    freeRev[e.To].Add(r);
                }
            }
        }

        var canExit = new HashSet<string>(StringComparer.Ordinal);
        var q = new Queue<string>();
        foreach (MapEdge entry in entries)
        {
            if (canExit.Add(entry.To))
            {
                q.Enqueue(entry.To);
            }
        }

        while (q.Count > 0)
        {
            string u = q.Dequeue();
            foreach (string prev in freeRev[u])
            {
                if (canExit.Add(prev))
                {
                    q.Enqueue(prev);
                }
            }
        }

        if (canExit.Count != pocketRooms.Count)
        {
            return false;
        }

        var canEnter = new HashSet<string>(StringComparer.Ordinal);
        foreach (MapEdge entry in entries)
        {
            if (canEnter.Add(entry.To))
            {
                q.Enqueue(entry.To);
            }
        }

        while (q.Count > 0)
        {
            string u = q.Dequeue();
            foreach (MapEdge e in map.From(u))
            {
                if (pocketRooms.Contains(e.To) && IsFree(e.Requires) && canEnter.Add(e.To))
                {
                    q.Enqueue(e.To);
                }
            }
        }

        return canEnter.Count == pocketRooms.Count;
    }
}
