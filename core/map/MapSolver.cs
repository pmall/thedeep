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
        MapItemReduction reduction = MapItemReduction.Analyze(map);

        // 1. Every reachable state, with how it was first reached (for softlock paths).
        SolverState start = Enter(map, map.Start, Ability.None, ItemSet.Empty, reduction);
        var parent = new Dictionary<SolverState, SolverState?> { [start] = null };
        var successors = new Dictionary<SolverState, List<SolverState>>();
        var queue = new Queue<SolverState>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            SolverState s = queue.Dequeue();
            ItemSet effective = reduction.LocalItems.Count > 0 ? s.Held.Union(reduction.LocalItems) : s.Held;
            var next = new List<SolverState>();
            foreach (MapEdge edge in map.From(s.Room))
            {
                if (reduction.MentionsOptional(edge.Requires) || !edge.Requires.IsMetBy(s.Owned, effective))
                {
                    continue;
                }

                SolverState t = Enter(map, edge.To, s.Owned, s.Held, reduction);
                next.Add(t);
                if (!parent.ContainsKey(t))
                {
                    parent[t] = s;
                    queue.Enqueue(t);
                }
            }

            successors[s] = next;
        }

        // 2. States that can still reach a goal: backward BFS from the goal states.
        var good = CanReach(parent.Keys, successors, parent.Keys.Where(s => map.Goals.Contains(s.Room)));

        var softlocks = parent.Keys.Where(s => !good.Contains(s))
            .OrderBy(s => PathTo(s, parent).Count).ThenBy(s => s.Room, StringComparer.Ordinal).ThenBy(s => s.Owned)
            .ThenBy(s => s.Held.ToString(), StringComparer.Ordinal)
            .Select(s => (s, (IReadOnlyList<SolverState>)PathTo(s, parent)))
            .ToList();

        var reachedRooms = new HashSet<string>(parent.Keys.Select(s => s.Room));
        foreach ((string item, MapItemReduction.OptionalPocket pocket) in reduction.OptionalPockets)
        {
            if (reduction.PocketReachable(map, item, pocket, parent.Keys, successors))
            {
                reachedRooms.UnionWith(pocket.Rooms);
            }
        }

        (List<Ability[]> orders, int count) = Orders(map, start, successors);
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

    /// <summary>Arriving in <paramref name="room"/> with <paramref name="owned"/> and <paramref name="held"/>: its relics and tracked items are taken.</summary>
    private static SolverState Enter(MapGraph map, string room, Ability owned, ItemSet held, MapItemReduction? reduction = null) =>
        new(room, map.RelicsIn(room).Aggregate(owned, (o, relic) => o | relic.Ability),
            map.ItemsIn(room).Where(item => reduction is null || !reduction.Ignored(item.Item))
                .Aggregate(held, (h, item) => h.With(item.Item)));

    /// <summary>What a state holds, for reports: <c> [MagGrip, Drill; CryptKey]</c>, empty for nothing.</summary>
    public static string Holding(Ability owned, ItemSet held) =>
        owned == Ability.None && held.Count == 0 ? ""
        : held.Count == 0 ? $" [{owned}]"
        : owned == Ability.None ? $" [{held}]"
        : $" [{owned}; {held}]";

    /// <summary>The states that can still reach one of <paramref name="goals"/>: backward BFS over the reversed edges, linear in states and edges.</summary>
    private static HashSet<T> CanReach<T>(IEnumerable<T> states, Dictionary<T, List<T>> successors, IEnumerable<T> goals)
        where T : struct
    {
        var reverse = new Dictionary<T, List<T>>();
        foreach (T s in states)
        {
            foreach (T next in successors[s])
            {
                if (!reverse.TryGetValue(next, out List<T>? from))
                {
                    reverse[next] = from = new List<T>();
                }

                from.Add(s);
            }
        }

        var good = new HashSet<T>();
        var queue = new Queue<T>();
        foreach (T g in goals)
        {
            if (good.Add(g))
            {
                queue.Enqueue(g);
            }
        }

        while (queue.Count > 0)
        {
            T at = queue.Dequeue();
            if (!reverse.TryGetValue(at, out List<T>? from))
            {
                continue;
            }

            foreach (T prev in from)
            {
                if (good.Add(prev))
                {
                    queue.Enqueue(prev);
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
    /// Every order in which all the map's abilities can be taken, computed directly over the already-explored state graph:
    /// condenses strongly connected components (reverse topological order) and propagates packed suffix orders backward, so
    /// states reached by different permutations of the same owned set share their remaining suffixes instead of re-exploring
    /// the map per permutation. Relics in one room are taken in the order they are listed.
    /// </summary>
    private static (List<Ability[]> Orders, int Count) Orders(MapGraph map, SolverState start, Dictionary<SolverState, List<SolverState>> successors)
    {
        Ability all = map.AbilitiesOnMap;
        Ability[] abilities = AbilitySet.All.Where(a => all.HasFlag(a)).ToArray();
        int count = abilities.Length;
        if (count == 0)
        {
            return (new List<Ability[]>(), 0);
        }

        var nibble = new Dictionary<Ability, ulong>(count);
        for (int i = 0; i < count; i++)
        {
            nibble[abilities[i]] = (ulong)(i + 1);
        }

        // Iterative Tarjan SCC over the reachable state graph: outputs components in reverse topological order (sinks first).
        List<List<SolverState>> components = StronglyConnected(start, successors, out Dictionary<SolverState, int> componentOf);
        var suffixes = new HashSet<ulong>[components.Count];
        var gained = new List<Ability>();
        for (int c = 0; c < components.Count; c++)
        {
            var set = new HashSet<ulong>();
            List<SolverState> members = components[c];
            if (members[0].Owned == all)
            {
                set.Add(0UL);
                suffixes[c] = set;
                continue;
            }

            foreach (SolverState u in members)
            {
                foreach (SolverState v in successors[u])
                {
                    int target = componentOf[v];
                    if (target == c || suffixes[target].Count == 0)
                    {
                        continue;
                    }

                    if (v.Owned == u.Owned)
                    {
                        set.UnionWith(suffixes[target]);
                    }
                    else
                    {
                        CollectNewRelics(map, v.Room, u.Owned, gained);
                        foreach (ulong tail in suffixes[target])
                        {
                            set.Add(Prepend(tail, gained, nibble));
                        }
                    }
                }
            }

            suffixes[c] = set;
        }

        CollectNewRelics(map, map.Start, Ability.None, gained);
        var orders = new List<Ability[]>();
        foreach (ulong tail in suffixes[componentOf[start]])
        {
            ulong full = Prepend(tail, gained, nibble);
            var order = new Ability[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = abilities[(int)(full & 0xFUL) - 1];
                full >>= 4;
            }

            orders.Add(order);
        }

        orders.Sort((a, b) => string.CompareOrdinal(string.Join(",", a), string.Join(",", b)));
        return (orders, count);
    }

    private static void CollectNewRelics(MapGraph map, string room, Ability alreadyOwned, List<Ability> result)
    {
        result.Clear();
        Ability seen = alreadyOwned;
        foreach (MapRelic relic in map.RelicsIn(room))
        {
            if ((seen & relic.Ability) == 0)
            {
                seen |= relic.Ability;
                result.Add(relic.Ability);
            }
        }
    }

    private static ulong Prepend(ulong tail, List<Ability> gained, Dictionary<Ability, ulong> nibble)
    {
        ulong code = tail;
        for (int i = gained.Count - 1; i >= 0; i--)
        {
            code = (code << 4) | nibble[gained[i]];
        }

        return code;
    }

    /// <summary>Iterative Tarjan strongly connected components from <paramref name="start"/>, returned in reverse topological order.</summary>
    private static List<List<SolverState>> StronglyConnected(SolverState start, Dictionary<SolverState, List<SolverState>> successors,
        out Dictionary<SolverState, int> componentOf)
    {
        var index = new Dictionary<SolverState, int>(successors.Count);
        var lowlink = new Dictionary<SolverState, int>(successors.Count);
        var onStack = new HashSet<SolverState>();
        var tarjan = new Stack<SolverState>();
        var frames = new Stack<(SolverState Node, int Edge)>();
        var components = new List<List<SolverState>>();
        componentOf = new Dictionary<SolverState, int>(successors.Count);
        int nextIndex = 0;

        index[start] = lowlink[start] = nextIndex++;
        tarjan.Push(start);
        onStack.Add(start);
        frames.Push((start, 0));

        while (frames.Count > 0)
        {
            (SolverState v, int edge) = frames.Pop();
            List<SolverState> edges = successors[v];
            if (edge < edges.Count)
            {
                frames.Push((v, edge + 1));
                SolverState w = edges[edge];
                if (!index.TryGetValue(w, out int wIndex))
                {
                    index[w] = lowlink[w] = nextIndex++;
                    tarjan.Push(w);
                    onStack.Add(w);
                    frames.Push((w, 0));
                }
                else if (onStack.Contains(w) && wIndex < lowlink[v])
                {
                    lowlink[v] = wIndex;
                }
            }
            else
            {
                if (lowlink[v] == index[v])
                {
                    int id = components.Count;
                    var component = new List<SolverState>();
                    while (true)
                    {
                        SolverState w = tarjan.Pop();
                        onStack.Remove(w);
                        componentOf[w] = id;
                        component.Add(w);
                        if (w.Equals(v))
                        {
                            break;
                        }
                    }

                    components.Add(component);
                }

                if (frames.Count > 0)
                {
                    SolverState parent = frames.Peek().Node;
                    if (lowlink[v] < lowlink[parent])
                    {
                        lowlink[parent] = lowlink[v];
                    }
                }
            }
        }

        return components;
    }
}
