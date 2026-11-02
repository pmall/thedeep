using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>A place in the game as the solver sees it: a room, and the abilities owned and items held on arriving there.</summary>
public readonly record struct SolverState(string Room, Ability Owned, ItemSet Held)
{
    public SolverState(string room, Ability owned) : this(room, owned, ItemSet.Empty)
    {
    }

    public override string ToString() => $"{Room}{MapSolver.Holding(Owned, Held)}";
}

/// <summary>
/// A place in the game as the inside solver sees it: a room, the way the pilgrim came in (an exit's name; "" for waking at the
/// start), and the abilities owned and items held on arriving.
/// </summary>
public readonly record struct WayState(string Room, string Entry, Ability Owned, ItemSet Held)
{
    public WayState(string room, string entry, Ability owned) : this(room, entry, owned, ItemSet.Empty)
    {
    }

    public override string ToString() => $"{Room} via {(Entry == "" ? "the start" : Entry)}{MapSolver.Holding(Owned, Held)}";
}

/// <summary>
/// What a pilgrim can do inside a room, come in through <see cref="WayState.Entry"/> with <see cref="WayState.Owned"/>: the abilities
/// it owns and the items it holds once it has taken the relics and items it can reach there, the exits it can reach (their
/// requirements still apply), and whether it reaches a goal. The geometry planner answers it for the built rooms; tests by hand.
/// </summary>
public readonly record struct RoomReach(Ability Owned, ItemSet Held, IReadOnlyCollection<string> Exits, bool Goal)
{
    public RoomReach(Ability owned, IReadOnlyCollection<string> exits, bool goal) : this(owned, ItemSet.Empty, exits, goal)
    {
    }
}

/// <summary>What the inside solver found.</summary>
public sealed class InsideReport
{
    public int ReachableStates { get; init; }

    public bool GoalReachable { get; init; }

    /// <summary>Reachable states from which no goal can be reached, each with the path (states) that leads there from the start.</summary>
    public IReadOnlyList<(WayState State, IReadOnlyList<WayState> Path)> Softlocks { get; init; } = Array.Empty<(WayState, IReadOnlyList<WayState>)>();

    /// <summary>Rooms no reachable state is in.</summary>
    public IReadOnlyList<string> UnenteredRooms { get; init; } = Array.Empty<string>();

    /// <summary>Relics no reachable state takes.</summary>
    public IReadOnlyList<MapRelic> UntakenRelics { get; init; } = Array.Empty<MapRelic>();

    /// <summary>Items no reachable state takes.</summary>
    public IReadOnlyList<MapItem> UntakenItems { get; init; } = Array.Empty<MapItem>();

    public bool SoftlockFree => GoalReachable && Softlocks.Count == 0;
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

    public IReadOnlyList<MapRelic> UnreachableRelics { get; init; } = Array.Empty<MapRelic>();

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
/// The "any order, proven on every build" solver (docs/long-term.md#any-order), pure. It explores every
/// state (room, abilities owned, items held) reachable from the start: crossing an exit needs its requirement; entering
/// a room takes its relics and items (both only ever help and are never spent, so taking them is never a choice). No softlock (docs/long-term.md#no-softlock): from
/// every reachable state some goal stays reachable (no softlock), else it reports the state and the path
/// there. Order freedom (docs/long-term.md#order-freedom): it lists the orders in which all the map's abilities can be collected.
/// </summary>
public static class MapSolver
{
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
            .ThenBy(s => s.Held.ToString(), StringComparer.Ordinal)
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
            UnreachableRelics = map.Relics.Where(i => !reachedRooms.Contains(i.Room)).ToList(),
            UnreachableItems = map.Items.Where(i => !reachedRooms.Contains(i.Room)).ToList(),
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
    /// <paramref name="inside"/> is called once per state, from several threads at once.
    /// </summary>
    public static InsideReport SolveInside(MapGraph map, Func<WayState, RoomReach> inside)
    {
        var start = new WayState(map.Start, "", Ability.None, ItemSet.Empty);
        var parent = new Dictionary<WayState, WayState?> { [start] = null };
        var successors = new Dictionary<WayState, List<WayState>>();
        var goals = new HashSet<WayState>();
        var taken = new HashSet<(string Room, Ability Ability)>();
        var held = new HashSet<(string Room, string Item)>();
        var level = new List<WayState> { start };
        while (level.Count > 0)
        {
            // One breadth-first level at a time, the rooms walked in parallel (the planner is the cost).
            var reach = new RoomReach[level.Count];
            System.Threading.Tasks.Parallel.For(0, level.Count, i => reach[i] = inside(level[i]));
            var next = new List<WayState>();
            for (int i = 0; i < level.Count; i++)
            {
                WayState s = level[i];
                RoomReach r = reach[i];
                if (r.Goal)
                {
                    goals.Add(s);
                }

                foreach (Ability gained in Enum.GetValues<Ability>().Where(a => a != Ability.None && (r.Owned & a) != 0 && (s.Owned & a) == 0))
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
                    var t = new WayState(edge.To, edge.ToExit, r.Owned, r.Held);
                    after.Add(t);
                    if (!parent.ContainsKey(t))
                    {
                        parent[t] = s;
                        next.Add(t);
                    }
                }

                successors[s] = after;
            }

            level = next;
        }

        var good = new HashSet<WayState>(goals);
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (WayState s in parent.Keys)
            {
                if (!good.Contains(s) && successors[s].Any(good.Contains))
                {
                    good.Add(s);
                    grew = true;
                }
            }
        }

        List<WayState> PathOf(WayState s)
        {
            var path = new List<WayState>();
            for (WayState? at = s; at is { } a; at = parent[a])
            {
                path.Add(a);
            }

            path.Reverse();
            return path;
        }

        return new InsideReport
        {
            ReachableStates = parent.Count,
            GoalReachable = good.Contains(start),
            Softlocks = parent.Keys.Where(s => !good.Contains(s))
                .Select(s => (s, (IReadOnlyList<WayState>)PathOf(s)))
                .OrderBy(p => p.Item2.Count).ThenBy(p => p.s.Room, StringComparer.Ordinal).ThenBy(p => p.s.Entry, StringComparer.Ordinal)
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
                if (edge.To == room && (best is not { } b || Count(s.Owned) < Count(b.Owned)
                        || (Count(s.Owned) == Count(b.Owned) && s.Held.Count < b.Held.Count)))
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

    private static int Count(Ability a) => System.Numerics.BitOperations.PopCount((uint)a);

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
