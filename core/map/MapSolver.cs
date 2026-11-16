using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// The "any order, proven on every build" solver (docs/long-term.md#any-order), pure. It explores every
/// state (room, abilities owned, items held) reachable from the start: crossing an exit needs its requirement; entering
/// a room takes its relics and items (both only ever help and are never spent, so taking them is never a choice). No softlock (docs/long-term.md#no-softlock): from
/// every reachable state some goal stays reachable (no softlock), else it reports the state and the path
/// there. Order freedom (docs/long-term.md#order-freedom): it lists the orders in which all the map's abilities can be collected.
/// </summary>
public static class MapSolver
{
    /// <summary>How many softlocks a check lists (the shortest paths first): the first ones show the way, the rest repeat them.</summary>
    public const int SoftlocksShown = 5;

    /// <summary>Solves <paramref name="map"/> from its start room: what can be reached, the softlocks, and the orders its
    /// abilities can be collected in.</summary>
    public static MapReport Solve(MapGraph map)
    {
        // 1. Every reachable state, with how it was first reached (for softlock paths).
        SolverState start = Enter(map, map.Start, Ability.None, ItemSet.Empty);
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
                if (!edge.Requires.IsMetBy(s.Owned, s.Held))
                {
                    continue;
                }

                SolverState t = Enter(map, edge.To, s.Owned, s.Held);
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
        var good = CanReach(parent.Keys, successors, parent.Keys.Where(s => map.Goals.Contains(s.Room)));

        var softlocks = parent.Keys.Where(s => !good.Contains(s))
            .OrderBy(s => PathTo(s, parent).Count).ThenBy(s => s.Room, StringComparer.Ordinal).ThenBy(s => s.Owned)
            .ThenBy(s => s.Held.ToString(), StringComparer.Ordinal)
            .Select(s => (s, (IReadOnlyList<SolverState>)PathTo(s, parent)))
            .ToList();

        var reachedRooms = new HashSet<string>(parent.Keys.Select(s => s.Room));
        (List<Ability[]> orders, int count) = Orders(map);
        var positions = new Dictionary<Ability, SortedSet<int>>();
        foreach (Ability a in AbilitySet.All.Where(a => map.AbilitiesOnMap.HasFlag(a)))
        {
            positions[a] = new SortedSet<int>(orders.Select(o => Array.IndexOf(o, a)).Where(i => i >= 0));
        }

        return new MapReport
        {
            ReachableStates = parent.Count,
            Softlocks = softlocks,
            GoalReachable = good.Contains(start),
            UnreachableRooms = map.Rooms.Where(r => !reachedRooms.Contains(r)).ToList(),
            Orders = orders,
            AbilityCount = count,
            Positions = positions,
        };
    }

    /// <summary>
    /// The solver with the rooms' insides (any order, without trusting the rooms): a room is not one place but what can be reached in
    /// it from each way in, with the abilities owned (<paramref name="inside"/>): which exits, which relics, the goal. Explores every
    /// (room, way in, abilities) reachable from waking in the start room; from each, some goal must stay reachable. Catches what the
    /// room-level <see cref="Solve"/> can't: a drop into the part of a room that only an ability leads out of, a relic behind a wall.
    /// <paramref name="inside"/> is called once per state, from several threads at once; it answers null for a way in the room doesn't
    /// have (an edge whose arrival exit is missing), and that way is not taken.
    /// </summary>
    public static InsideReport SolveInside(MapGraph map, Func<WayState, RoomReach?> inside)
    {
        var start = new WayState(map.Start, new WayEntry.Waking(), Ability.None, ItemSet.Empty);
        var parent = new Dictionary<WayState, WayState?> { [start] = null };
        var successors = new Dictionary<WayState, List<WayState>>();
        var goals = new HashSet<WayState>();
        var taken = new HashSet<(string Room, Ability Ability)>();
        var held = new HashSet<(string Room, string Item)>();
        var missing = new HashSet<WayState>();
        var level = new List<WayState> { start };
        while (level.Count > 0)
        {
            // One breadth-first level at a time, the rooms walked in parallel (the planner is the cost).
            var reach = new RoomReach?[level.Count];
            System.Threading.Tasks.Parallel.For(0, level.Count, i => reach[i] = inside(level[i]));
            var next = new List<WayState>();
            for (int i = 0; i < level.Count; i++)
            {
                WayState s = level[i];
                if (reach[i] is not { } r)
                {
                    parent.Remove(s);
                    missing.Add(s);
                    continue;
                }

                if (r.Goal)
                {
                    goals.Add(s);
                }

                foreach (Ability gained in AbilitySet.All.Where(a => (r.Owned & a) != 0 && (s.Owned & a) == 0))
                {
                    taken.Add((s.Room, gained));
                }

                foreach (string item in r.Held.Ids.Where(i => !s.Held.Contains(i)))
                {
                    held.Add((s.Room, item));
                }

                var after = new List<WayState>();
                foreach (MapEdge edge in map.From(s.Room).Where(e => r.Exits.Contains(e.FromExit) && e.Requires.IsMetBy(r.Owned, r.Held)))
                {
                    var t = new WayState(edge.To, new WayEntry.Through(edge.ToExit), r.Owned, r.Held);
                    after.Add(t);
                    if (!parent.ContainsKey(t) && !missing.Contains(t))
                    {
                        parent[t] = s;
                        next.Add(t);
                    }
                }

                successors[s] = after;
            }

            level = next;
        }

        var good = CanReach(parent.Keys, successors, goals);

        return new InsideReport
        {
            ReachableStates = parent.Count,
            GoalReachable = good.Contains(start),
            Softlocks = parent.Keys.Where(s => !good.Contains(s))
                .Select(s => (s, (IReadOnlyList<WayState>)PathTo(s, parent)))
                .OrderBy(p => p.Item2.Count).ThenBy(p => p.s.Room, StringComparer.Ordinal).ThenBy(p => p.s.Entry.ToString(), StringComparer.Ordinal)
                .ThenBy(p => p.s.Owned).ThenBy(p => p.s.Held.ToString(), StringComparer.Ordinal).ToList(),
            UntakenRelics = map.Relics.Where(relic => !taken.Contains((relic.Room, relic.Ability))).ToList(),
            UntakenItems = map.Items.Where(item => !held.Contains((item.Room, item.Item))).ToList(),
            UnenteredRooms = map.Rooms.Where(r => !parent.Keys.Any(s => s.Room == r)).ToList(),
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

        SolverState start = Enter(map, map.Start, Ability.None, ItemSet.Empty);
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

            foreach (MapEdge edge in map.From(s.Room).Where(e => e.To != avoiding && e.Requires.IsMetBy(s.Owned, s.Held)))
            {
                SolverState t = Enter(map, edge.To, s.Owned, s.Held);
                if (seen.Add(t))
                {
                    queue.Enqueue(t);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The fewest abilities (then items) a pilgrim can hold on arriving in <paramref name="room"/> from the start, before taking that
    /// room's own relics and items (so a relic room is tried with its relic still lying there); ties go to the shortest route. Null if
    /// no route reaches it. <c>--room</c> starts with these, so a room behind gates can be tried the way a player would come to it.
    /// </summary>
    public static (Ability Owned, ItemSet Held)? Arrival(MapGraph map, string room)
    {
        if (room == map.Start)
        {
            return (Ability.None, ItemSet.Empty);
        }

        SolverState start = Enter(map, map.Start, Ability.None, ItemSet.Empty);
        var seen = new HashSet<SolverState> { start };
        var queue = new Queue<SolverState>();
        queue.Enqueue(start);
        (Ability Owned, ItemSet Held)? best = null;
        while (queue.Count > 0)
        {
            SolverState s = queue.Dequeue();
            foreach (MapEdge edge in map.From(s.Room).Where(e => e.Requires.IsMetBy(s.Owned, s.Held)))
            {
                if (edge.To == room && (best is not { } b || AbilitySet.Count(s.Owned) < AbilitySet.Count(b.Owned)
                        || (AbilitySet.Count(s.Owned) == AbilitySet.Count(b.Owned) && s.Held.Count < b.Held.Count)))
                {
                    best = (s.Owned, s.Held);
                }

                SolverState t = Enter(map, edge.To, s.Owned, s.Held);
                if (seen.Add(t))
                {
                    queue.Enqueue(t);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// A shortest route (fewest ways taken) from the start to a goal, as the ways to take in order (abilities picked up from
    /// the relics of the rooms passed through, as <see cref="Solve"/> does); null if no goal can be reached. The playthrough
    /// check walks it.
    /// </summary>
    public static IReadOnlyList<MapEdge>? Route(MapGraph map)
    {
        SolverState start = Enter(map, map.Start, Ability.None, ItemSet.Empty);
        var parent = new Dictionary<SolverState, (SolverState From, MapEdge Edge)?> { [start] = null };
        var queue = new Queue<SolverState>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            SolverState s = queue.Dequeue();
            if (map.Goals.Contains(s.Room))
            {
                var route = new List<MapEdge>();
                for (SolverState at = s; parent[at] is { } p; at = p.From)
                {
                    route.Add(p.Edge);
                }

                route.Reverse();
                return route;
            }

            foreach (MapEdge edge in map.From(s.Room).Where(e => e.Requires.IsMetBy(s.Owned, s.Held)))
            {
                SolverState t = Enter(map, edge.To, s.Owned, s.Held);
                if (!parent.ContainsKey(t))
                {
                    parent[t] = (s, edge);
                    queue.Enqueue(t);
                }
            }
        }

        return null;
    }

    /// <summary>Arriving in <paramref name="room"/> with <paramref name="owned"/> and <paramref name="held"/>: its relics and items are taken.</summary>
    private static SolverState Enter(MapGraph map, string room, Ability owned, ItemSet held) =>
        new(room, map.RelicsIn(room).Aggregate(owned, (o, relic) => o | relic.Ability),
            map.ItemsIn(room).Aggregate(held, (h, item) => h.With(item.Item)));

    /// <summary>What a state holds, for reports: <c> [MagGrip, Drill; CryptKey]</c>, empty for nothing.</summary>
    public static string Holding(Ability owned, ItemSet held) =>
        owned == Ability.None && held.Count == 0 ? ""
        : held.Count == 0 ? $" [{owned}]"
        : owned == Ability.None ? $" [{held}]"
        : $" [{owned}; {held}]";

    /// <summary>The states that can still reach one of <paramref name="goals"/>: grown backward from them until nothing changes.</summary>
    private static HashSet<T> CanReach<T>(IEnumerable<T> states, Dictionary<T, List<T>> successors, IEnumerable<T> goals)
        where T : struct
    {
        var good = new HashSet<T>(goals);
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (T s in states)
            {
                if (!good.Contains(s) && successors[s].Any(good.Contains))
                {
                    good.Add(s);
                    grew = true;
                }
            }
        }

        return good;
    }

    /// <summary>The way the search first reached <paramref name="s"/>, from its start.</summary>
    private static List<T> PathTo<T>(T s, Dictionary<T, T?> parent)
        where T : struct
    {
        var path = new List<T>();
        for (T? at = s; at is { } a; at = parent[a])
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
        int count = AbilitySet.Count(all);
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var result = new List<Ability[]>();
        if (count == 0)
        {
            return (result, 0);
        }

        var seen = new HashSet<(string, string, ItemSet)>();
        var queue = new Queue<(string Room, List<Ability> Taken, ItemSet Held)>();

        void Visit(string room, List<Ability> taken, ItemSet held)
        {
            held = map.ItemsIn(room).Aggregate(held, (h, item) => h.With(item.Item));
            var now = new List<Ability>(taken);
            foreach (MapRelic item in map.RelicsIn(room))
            {
                if (!now.Contains(item.Ability))
                {
                    now.Add(item.Ability);
                }
            }

            string key = string.Join(",", now);
            if (!seen.Add((room, key, held)))
            {
                return;
            }

            if (now.Count == count && found.Add(key))
            {
                result.Add(now.ToArray());
            }

            queue.Enqueue((room, now, held));
        }

        Visit(map.Start, new List<Ability>(), ItemSet.Empty);
        while (queue.Count > 0)
        {
            (string room, List<Ability> taken, ItemSet held) = queue.Dequeue();
            Ability owned = taken.Aggregate(Ability.None, (o, a) => o | a);
            foreach (MapEdge edge in map.From(room).Where(e => e.Requires.IsMetBy(owned, held)))
            {
                Visit(edge.To, taken, held);
            }
        }

        return (result, count);
    }
}
