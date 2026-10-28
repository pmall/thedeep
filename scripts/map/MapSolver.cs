using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>A place in the game as the solver sees it: a room, and the abilities owned on arriving there.</summary>
public readonly record struct SolverState(string Room, Ability Owned)
{
    public override string ToString() => Owned == Ability.None ? Room : $"{Room} [{Owned}]";
}

/// <summary>What the solver found.</summary>
public sealed class MapReport
{
    /// <summary>States the pilgrim can reach from the start.</summary>
    public int ReachableStates { get; init; }

    /// <summary>Reachable states from which no goal can be reached, each with the path (states) that leads there from the start.</summary>
    public IReadOnlyList<(SolverState State, IReadOnlyList<SolverState> Path)> Softlocks { get; init; } = Array.Empty<(SolverState, IReadOnlyList<SolverState>)>();

    public bool GoalReachable { get; init; }

    public IReadOnlyList<string> UnreachableRooms { get; init; } = Array.Empty<string>();

    public IReadOnlyList<MapItem> UnreachableItems { get; init; } = Array.Empty<MapItem>();

    /// <summary>The orders in which every ability on the map can be collected.</summary>
    public IReadOnlyList<Ability[]> Orders { get; init; } = Array.Empty<Ability[]>();

    /// <summary>How many abilities lie on the map (orders are permutations of them).</summary>
    public int AbilityCount { get; init; }

    /// <summary>For each ability on the map, the positions (0-based) it takes in some achievable order.</summary>
    public IReadOnlyDictionary<Ability, SortedSet<int>> Positions { get; init; } = new Dictionary<Ability, SortedSet<int>>();

    public bool SoftlockFree => GoalReachable && Softlocks.Count == 0;

    /// <summary>All permutations of the abilities on the map: n!.</summary>
    public int PossibleOrders => Enumerable.Range(1, Math.Max(1, AbilityCount)).Aggregate(1, (f, k) => f * k);
}

/// <summary>
/// The "any order, proven on every build" solver (docs/long-term.md, objective 2), pure. It explores every
/// state (room, abilities owned) reachable from the start: crossing an exit needs its requirement; entering
/// a room takes its relics (abilities only ever help, so taking them is never a choice). Guarantee 1: from
/// every reachable state some goal stays reachable (no softlock), else it reports the state and the path
/// there. Guarantee 2: it lists the orders in which all the map's abilities can be collected.
/// </summary>
public static class MapSolver
{
    public static MapReport Solve(MapGraph map)
    {
        // 1. Every reachable state, with how it was first reached (for softlock paths).
        SolverState start = Enter(map, map.Start, Ability.None);
        var parent = new Dictionary<SolverState, SolverState?> { [start] = null };
        var successors = new Dictionary<SolverState, List<SolverState>>();
        var queue = new Queue<SolverState>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            SolverState s = queue.Dequeue();
            var next = new List<SolverState>();
            foreach (MapEdge edge in map.From(s.Room))
            {
                if (!edge.Requires.IsMetBy(s.Owned))
                {
                    continue;
                }

                SolverState t = Enter(map, edge.To, s.Owned);
                next.Add(t);
                if (!parent.ContainsKey(t))
                {
                    parent[t] = s;
                    queue.Enqueue(t);
                }
            }

            successors[s] = next;
        }

        // 2. States that can still reach a goal: grown backward from the goal states until nothing changes.
        var good = new HashSet<SolverState>(parent.Keys.Where(s => map.Goals.Contains(s.Room)));
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (SolverState s in parent.Keys)
            {
                if (!good.Contains(s) && successors[s].Any(good.Contains))
                {
                    good.Add(s);
                    grew = true;
                }
            }
        }

        var softlocks = parent.Keys.Where(s => !good.Contains(s))
            .OrderBy(s => PathTo(s, parent).Count).ThenBy(s => s.Room, StringComparer.Ordinal).ThenBy(s => s.Owned)
            .Select(s => (s, (IReadOnlyList<SolverState>)PathTo(s, parent)))
            .ToList();

        var reachedRooms = new HashSet<string>(parent.Keys.Select(s => s.Room));
        (List<Ability[]> orders, int count) = Orders(map);
        var positions = new Dictionary<Ability, SortedSet<int>>();
        foreach (Ability a in Enum.GetValues<Ability>().Where(a => a != Ability.None && map.AbilitiesOnMap.HasFlag(a)))
        {
            positions[a] = new SortedSet<int>(orders.Select(o => Array.IndexOf(o, a)).Where(i => i >= 0));
        }

        return new MapReport
        {
            ReachableStates = parent.Count,
            Softlocks = softlocks,
            GoalReachable = good.Contains(start),
            UnreachableRooms = map.Rooms.Where(r => !reachedRooms.Contains(r)).ToList(),
            UnreachableItems = map.Items.Where(i => !reachedRooms.Contains(i.Room)).ToList(),
            Orders = orders,
            AbilityCount = count,
            Positions = positions,
        };
    }

    /// <summary>
    /// Whether some state in one of <paramref name="targets"/> can be reached from the start without ever entering
    /// <paramref name="avoiding"/> (for story milestones: a room no route can skip).
    /// </summary>
    public static bool Reaches(MapGraph map, IEnumerable<string> targets, string avoiding)
    {
        var goals = new HashSet<string>(targets);
        if (map.Start == avoiding)
        {
            return false;
        }

        SolverState start = Enter(map, map.Start, Ability.None);
        var seen = new HashSet<SolverState> { start };
        var queue = new Queue<SolverState>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            SolverState s = queue.Dequeue();
            if (goals.Contains(s.Room))
            {
                return true;
            }

            foreach (MapEdge edge in map.From(s.Room).Where(e => e.To != avoiding && e.Requires.IsMetBy(s.Owned)))
            {
                SolverState t = Enter(map, edge.To, s.Owned);
                if (seen.Add(t))
                {
                    queue.Enqueue(t);
                }
            }
        }

        return false;
    }

    /// <summary>Arriving in <paramref name="room"/> with <paramref name="owned"/>: its relics are taken.</summary>
    private static SolverState Enter(MapGraph map, string room, Ability owned) =>
        new(room, map.In(room).Aggregate(owned, (o, item) => o | item.Ability));

    private static List<SolverState> PathTo(SolverState s, Dictionary<SolverState, SolverState?> parent)
    {
        var path = new List<SolverState>();
        for (SolverState? at = s; at is { } a; at = parent[a])
        {
            path.Add(a);
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Every order in which all the map's abilities can be taken: explores (room, abilities taken in order),
    /// relics in one room taken in the order they are listed.
    /// </summary>
    private static (List<Ability[]> Orders, int Count) Orders(MapGraph map)
    {
        Ability all = map.AbilitiesOnMap;
        int count = System.Numerics.BitOperations.PopCount((uint)all);
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var result = new List<Ability[]>();
        if (count == 0)
        {
            return (result, 0);
        }

        var seen = new HashSet<(string, string)>();
        var queue = new Queue<(string Room, List<Ability> Taken)>();

        void Visit(string room, List<Ability> taken)
        {
            var now = new List<Ability>(taken);
            foreach (MapItem item in map.In(room))
            {
                if (!now.Contains(item.Ability))
                {
                    now.Add(item.Ability);
                }
            }

            string key = string.Join(",", now);
            if (!seen.Add((room, key)))
            {
                return;
            }

            if (now.Count == count && found.Add(key))
            {
                result.Add(now.ToArray());
            }

            queue.Enqueue((room, now));
        }

        Visit(map.Start, new List<Ability>());
        while (queue.Count > 0)
        {
            (string room, List<Ability> taken) = queue.Dequeue();
            Ability owned = taken.Aggregate(Ability.None, (o, a) => o | a);
            foreach (MapEdge edge in map.From(room).Where(e => e.Requires.IsMetBy(owned)))
            {
                Visit(edge.To, taken);
            }
        }

        return (result, count);
    }
}
