using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TheDeep;

/// <summary>
/// The built world proven as a whole (docs/long-term.md#any-order), pure: given each room's part of the map (its exits as
/// edges, its relics and goals, its links to the rooms it leads to) and its <see cref="RoomShape"/>, it checks that
/// - the rooms placed from their exits line up around every loop and don't overlap (<see cref="MapLayout"/>);
/// - every room is reachable from the start, a goal is, no reachable state is a softlock, and enough ability orders are achievable
///   (<see cref="MapSolver"/>, the world's <see cref="MapTargets"/>);
/// - the geometry agrees with every requirement (<see cref="CheckGeometry"/>), the map holds from inside the rooms
///   (<see cref="CheckInside"/>), and a clumsier pilgrim reaches all the same (room for error).
/// The room check (an adapter) extracts the rooms from their scenes and prints what this finds.
/// </summary>
public sealed class WorldCheck
{
    /// <summary>Two rooms' bounds may overlap this much on the map (their walls at a shared doorway), meters.</summary>
    public const float DoorwayOverlap = 6f;

    /// <summary>The abilities the geometry check simulates (all five).</summary>
    private const Ability Simulated = Ability.MagGrip | Ability.Harpoon | Ability.Drill | Ability.Ballast | Ability.HeatPlating;

    /// <summary>Each simulated ability on its own.</summary>
    private static readonly Ability[] Singles = { Ability.MagGrip, Ability.Harpoon, Ability.Drill, Ability.Ballast, Ability.HeatPlating };

    /// <summary>The simulated abilities taken together, fewest first, for ways that need more than one.</summary>
    private static readonly Ability[] Combined = Enumerable.Range(1, (int)Simulated)
        .Select(i => (Ability)i).Where(a => (a & ~Simulated) == 0 && BitOperations.PopCount((uint)a) > 1)
        .OrderBy(a => BitOperations.PopCount((uint)a)).ToArray();

    private readonly PlannerLimits _limits;
    private readonly List<string> _problems = new();
    private readonly List<(string Room, Requirement Requires)> _sealed = new();
    private readonly List<MapLink> _links = new();
    private readonly Dictionary<string, (Vector2 Min, Vector2 Max)> _bounds = new();
    private readonly Dictionary<string, RoomShape> _shapes = new();
    private readonly ConcurrentDictionary<(string Room, Ability With, bool Clumsy), Lazy<ReachPlanner>> _planners = new();
    private readonly ConcurrentDictionary<ReachKey, Lazy<HashSet<int>>> _reaches = new();
    private readonly Dictionary<string, Ability> _matters = new();
    private readonly Dictionary<string, List<Box>> _triggers = new();

    /// <summary>A check that simulates the pilgrim's moves with <paramref name="limits"/>.</summary>
    public WorldCheck(PlannerLimits limits) => _limits = limits;

    /// <summary>The built map: rooms, exits (one edge each, for leaving through it), relics and goals.</summary>
    public MapGraph Graph { get; } = new();

    /// <summary>The built rooms' sealed exits (to rooms not built yet) and their requirements.</summary>
    public IReadOnlyList<(string Room, Requirement Requires)> Sealed => _sealed;

    /// <summary>Everything found wrong so far, one line each; empty when the world passes.</summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>A room in the game, with its camera bounds and its shape.</summary>
    public void AddRoom(string name, Vector2 boundsMin, Vector2 boundsMax, RoomShape shape)
    {
        Graph.AddRoom(name);
        _bounds[name] = (boundsMin, boundsMax);
        _shapes[name] = shape;
        _matters[name] = Matters(shape);
        _triggers[name] = shape.Exits.Select(e => e.Threshold.Trigger).Concat(shape.Relics.Select(r => r.Area))
            .Concat(shape.Items.Select(i => i.Area)).Concat(shape.Goals).ToList();
    }

    /// <summary>
    /// An exit of a built room leading to another built room: its edge on the map, and, where the arrival exit is known, the link
    /// that places the two rooms side by side.
    /// </summary>
    public void AddExit(MapEdge edge, Vector2 from, Vector2? arrival)
    {
        Graph.AddEdge(edge);
        if (arrival is { } to)
        {
            _links.Add(new MapLink(edge.From, edge.FromExit, from, edge.To, edge.ToExit, to));
        }
    }

    /// <summary>An exit of the built room <paramref name="room"/> leading to a room not built yet, and what it needs.</summary>
    public void AddSealed(string room, Requirement requires) => _sealed.Add((room, requires));

    /// <summary>
    /// Solves the map from <paramref name="start"/> and lays it out, holding it to <paramref name="targets"/>; adds problems and
    /// returns the summary (with <paramref name="report"/>, the whole graph and every order too).
    /// </summary>
    public string Check(string start, MapTargets targets, bool report)
    {
        if (!Graph.Rooms.Contains(start))
        {
            _problems.Add($"map: the start room {start} is not a room in the game");
            return "";
        }

        Graph.Start = start;
        if (Graph.Goals.Count == 0)
        {
            _problems.Add("map: no room holds a MapGoal (where the journey ends)");
        }

        MapLayout.Result layout = MapLayout.Place(start, _links, _bounds, DoorwayOverlap);
        foreach (string problem in layout.Problems)
        {
            _problems.Add($"map layout: {problem}");
        }

        _problems.AddRange(Graph.ItemProblems(_sealed.Select(s => s.Requires)).Select(p => $"map: {p}"));
        CheckGeometry();
        string inside = CheckInside();
        MapReport solved = MapSolver.Solve(Graph);
        foreach (string room in solved.UnreachableRooms)
        {
            _problems.Add($"map: {room} can't be reached from {start}");
        }

        if (!solved.GoalReachable)
        {
            _problems.Add($"map: no goal can be reached from {start}");
        }

        foreach ((SolverState state, IReadOnlyList<SolverState> path) in solved.Softlocks.Take(5))
        {
            _problems.Add($"map: softlock at {state}: no goal can be reached from there; reached by {string.Join(" → ", path)}");
        }

        if (solved.AbilityCount == Singles.Length && solved.Orders.Count < targets.Orders)
        {
            _problems.Add($"map: only {solved.Orders.Count} of {solved.PossibleOrders} ability orders are achievable (target {targets.Orders})");
        }

        var text = new StringBuilder();
        if (report && _problems.Count > 0)
        {
            AppendPlacements(text, layout);
            return text.ToString();
        }

        text.Append(CultureInfo.InvariantCulture, $"MAP OK: {Graph.Rooms.Count} rooms, {Graph.Edges.Count} exits, {Graph.Relics.Count} relics, {Graph.Items.Count} item{(Graph.Items.Count == 1 ? "" : "s")}, ")
            .Append(CultureInfo.InvariantCulture, $"{solved.ReachableStates} states, no softlock, ")
            .Append(solved.AbilityCount == 0 ? "no abilities on the map" : $"{solved.Orders.Count} of {solved.PossibleOrders} ability orders");
        foreach ((Ability ability, SortedSet<int> at) in solved.Positions)
        {
            text.Append(CultureInfo.InvariantCulture, $"; {ability} at {string.Join("/", at.Select(i => i + 1))}");
        }

        text.Append('\n').Append(inside);
        if (report)
        {
            text.Append("\nmap graph (leaving through an exit needs its requirement):");
            foreach (MapEdge e in Graph.Edges)
            {
                string needs = e.Requires.ToString();
                text.Append(CultureInfo.InvariantCulture, $"\n  {e.From}:{e.FromExit} → {e.To}:{e.ToExit}{(needs == "" ? "" : $"  needs {needs}")}");
            }

            foreach (MapRelic item in Graph.Relics)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n  relic {item.Ability} in {item.Room} ({item.Name})");
            }

            foreach (MapItem item in Graph.Items)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n  item {item.Item} in {item.Room} ({item.Name})");
            }

            AppendPlacements(text, layout);
            foreach (Ability[] order in solved.Orders)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n  order: {string.Join(" → ", order)}");
            }
        }

        return text.ToString();
    }

    private static void AppendPlacements(StringBuilder text, MapLayout.Result layout)
    {
        foreach ((string room, Vector2 offset) in layout.Offsets.OrderBy(o => o.Key, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"\n  {room} at ({offset.X:0.##}, {offset.Y:0.##})");
        }
    }

    /// <summary>
    /// What each way into <paramref name="room"/> reaches (`--reach-report`), to see where a planner stops: with
    /// <paramref name="with"/> the abilities every way is walked with, instead of what the way guarantees; with
    /// <paramref name="clumsy"/>, the clumsier pilgrim's reach. Then every ledge of the room.
    /// </summary>
    public IEnumerable<string> ReachReport(string room, Ability? with, bool clumsy)
    {
        if (!_shapes.TryGetValue(room, out RoomShape? shape))
        {
            yield return $"REACH {room}: not a room in the game";
            yield break;
        }

        var triggers = shape.Exits.Select(e => e.Threshold.Trigger).ToList();
        List<WayIn> ways = WaysIn(room);
        foreach (WayIn way in ways)
        {
            ReachPlanner p = Planner(room, with ?? way.Guaranteed, clumsy);
            var (exits, ledges) = p.Reach(way.At, way.Velocity, triggers, way.Steer);
            yield return $"REACH {room} {way.What} [{with ?? way.Guaranteed}]: exits " +
                         string.Join(", ", exits.Select(t => shape.Exits[t].Name)) + "; ledges " +
                         string.Join(" ", ledges.OrderBy(l => p.Ledges[l].Y).Select(l => Describe(p.Ledges[l])));
        }

        ReachPlanner none = Planner(room, Ability.None);
        yield return $"REACH {room} all ledges: " + string.Join(" ", none.Ledges.OrderBy(l => l.Y).Select(Describe));
    }

    private static string Describe(Ledge l) => $"{(l.Swim ? "~" : "")}{l.Y:0.##}@{l.MinX:0.##}..{l.MaxX:0.##}";

    /// <summary>
    /// The planner for a room with some abilities (cached): Mag-grip walks its metal, the Harpoon zips between its anchors, the Drill
    /// bores what it reaches (the cracked blocks gone), Ballast sinks and rises in its water, Heat plating walks through its heat.
    /// </summary>
    private ReachPlanner Planner(string room, Ability with, bool clumsy = false) =>
        _planners.GetOrAdd((room, with & _matters[room], clumsy), key => new Lazy<ReachPlanner>(() => NewPlanner(key.Room, key.With, key.Clumsy))).Value;

    private ReachPlanner NewPlanner(string room, Ability g, bool clumsy) => For(_shapes[room], clumsy ? _limits with { Move = _limits.Move.Clumsy() } : _limits, g);

    /// <summary>
    /// The planner for a room of <paramref name="shape"/> with the abilities <paramref name="g"/>, simulated with <paramref name="limits"/>:
    /// Mag-grip walks its metal, the Harpoon zips between its anchors, the Drill bores what it reaches, Ballast sinks and rises in its
    /// water, Heat plating walks through its heat (slag stays out of reach, plated or not).
    /// </summary>
    public static ReachPlanner For(RoomShape shape, PlannerLimits limits, Ability g)
    {
        bool grip = (g & Ability.MagGrip) != 0;
        return new ReachPlanner(limits.Move, (g & Ability.Drill) != 0 ? shape.Sound : shape.Solids,
            (g & Ability.Harpoon) != 0 ? limits.Harpoon : null, shape.Anchors, shape.Lifts, limits.Water, shape.Waters,
            ballast: (g & Ability.Ballast) != 0, hazards: (g & Ability.HeatPlating) != 0 ? shape.Slag : shape.Heat.Concat(shape.Slag),
            grip: grip ? limits.Grip : null, metal: grip ? shape.Metal : null);
    }

    /// <summary>
    /// The simulated abilities that change anything in a room: Mag-grip where there is metal, the Harpoon where there are anchors, the
    /// Drill where there is cracked rock, Ballast where there is water, Heat plating where there is heat. The planner can't tell the
    /// others from not owning them, so the walks are shared between them (the check walks a room once per set that matters).
    /// </summary>
    private static Ability Matters(RoomShape shape) =>
        (shape.Metal.Count > 0 ? Ability.MagGrip : Ability.None)
        | (shape.Anchors.Count > 0 ? Ability.Harpoon : Ability.None)
        | (shape.Sound.Count != shape.Solids.Count ? Ability.Drill : Ability.None)
        | (shape.Waters.Count > 0 ? Ability.Ballast : Ability.None)
        | (shape.Heat.Count > 0 ? Ability.HeatPlating : Ability.None);

    /// <summary>One walk of a room: where the body comes in, the abilities that matter there, and which pilgrim.</summary>
    private readonly record struct ReachKey(string Room, Vector2 At, Vector2 Velocity, int Steer, Ability With, bool Clumsy);

    /// <summary>
    /// The triggers a walk of <paramref name="room"/> reaches (its exits by index, then its relics, its items, its goals), walked once per
    /// way in, abilities that matter there and pilgrim (cached: the geometry, inside and room-for-error checks share the walks).
    /// </summary>
    private HashSet<int> Reached(string room, WayIn way, Ability with, bool clumsy = false) =>
        _reaches.GetOrAdd(new ReachKey(room, way.At, way.Velocity, way.Steer, with & _matters[room], clumsy),
            key => new Lazy<HashSet<int>>(() => Planner(key.Room, key.With, key.Clumsy).Reach(key.At, key.Velocity, _triggers[key.Room], key.Steer).Triggers)).Value;

    /// <summary>
    /// A way into a room: arriving through an exit (<see cref="Exit"/>, its index) by an edge needing a requirement, or waking
    /// (Exit −1); where and how the body enters, and the abilities every option of the requirement holds (<see cref="Guaranteed"/>:
    /// whoever arrives through a way only the Drill opens has the Drill, and bores what it reaches there).
    /// </summary>
    private sealed record WayIn(string What, Vector2 At, Vector2 Velocity, int Steer, int Exit, bool Base, Ability Guaranteed);

    /// <summary>Arriving through an exit (<see cref="ExitShape.Entry"/>), by an edge needing <paramref name="requires"/>.</summary>
    private WayIn Arriving(RoomShape shape, int index, Requirement requires)
    {
        ExitShape x = shape.Exits[index];
        (Vector2 at, Vector2 velocity, int steer) = x.Entry(_limits, shape.Solids);
        Requirement body = requires.WithoutItems();
        return new WayIn($"arriving through {x.Name}", at, velocity, steer, index, body.IsMetBy(Ability.None),
            body.Options.Aggregate(Simulated, (all, option) => all & option.Abilities));
    }

    private List<WayIn> WaysIn(string room)
    {
        RoomShape shape = _shapes[room];
        var ways = new List<WayIn>();
        foreach (MapEdge edge in Graph.Edges.Where(e => e.To == room))
        {
            int index = shape.Exits.FindIndex(x => x.Name == edge.ToExit);
            if (index >= 0)
            {
                ways.Add(Arriving(shape, index, edge.Requires));
            }
        }

        foreach (Vector2 wake in shape.Wakes)
        {
            ways.Add(new WayIn($"waking at ({wake.X:0.#}, {wake.Y:0.#})", wake, Vector2.Zero, 0, -1, true, Ability.None));
        }

        return ways;
    }

    private static string Describe(Ability a) => a switch
    {
        Ability.MagGrip => "Mag-grip",
        Ability.Harpoon => "the Harpoon",
        Ability.Drill => "the Drill",
        Ability.Ballast => "Ballast",
        Ability.HeatPlating => "Heat plating",
        _ => $"{a} together",
    };

    /// <summary>
    /// The geometry sanity check: the base moveset, simulated with the live tuning (<see cref="ReachPlanner"/>), must reach every
    /// exit marked for the base moveset from some way into its room, and must not reach an exit that needs an ability from a way in
    /// that needed none (that requirement would be a lie, or the geometry drifted from it). An exit an ability opens must be reached
    /// by the planner with that ability (and what the way in guarantees: abilities accumulate) from some way in; for a requirement
    /// with several options, by each option on its own; for abilities needed together, by all of them. All five are simulated:
    /// Mag-grip walks metal, the Harpoon zips between anchors, the Drill bores cracked rock, Ballast sinks and rises in water, Heat
    /// plating walks through heat. Sealed exits (rooms not built yet) are held to their requirement too, so a gate is right before
    /// the room behind it exists. Lifts are ridden from their lower stop.
    /// </summary>
    private void CheckGeometry()
    {
        var rooms = _shapes.Keys.Order(StringComparer.Ordinal).ToList();
        var found = new List<string>[rooms.Count];
        Parallel.For(0, rooms.Count, n => found[n] = CheckGeometry(rooms[n], _shapes[rooms[n]]));
        foreach (List<string> problems in found)
        {
            _problems.AddRange(problems);
        }
    }

    /// <summary>The geometry check of one room (<see cref="CheckGeometry()"/>): its problems.</summary>
    private List<string> CheckGeometry(string room, RoomShape shape)
    {
        var problems = new List<string>();
        List<WayIn> ways = WaysIn(room);
        HashSet<int> Reached(int w, Ability with) => this.Reached(room, ways[w], (with | ways[w].Guaranteed) & Simulated);
        for (int i = 0; i < shape.Exits.Count; i++)
        {
            ExitShape x = shape.Exits[i];
            var others = Enumerable.Range(0, ways.Count).Where(w => ways[w].Exit != i).ToList();
            if (others.Count == 0 || !Requirement.TryParse(x.Requires, out Requirement written, out _))
            {
                continue;
            }

            Requirement r = written.WithoutItems(); // an item opens a door at the exit itself: the body must reach the door

            if (r.IsMetBy(Ability.None))
            {
                if (!x.Sealed && !others.Any(w => Reached(w, Ability.None).Contains(i)))
                {
                    problems.Add($"{room}: exit {x.Name} needs nothing, but the base moveset reaches it from no way into the room");
                }

                continue;
            }

            // Each way the requirement can be met: every single ability that meets it, else the fewest abilities together.
            var options = Singles.Where(a => r.IsMetBy(a)).ToList();
            if (options.Count == 0 && Combined.FirstOrDefault(c => r.IsMetBy(c)) is var together && together != Ability.None)
            {
                options.Add(together);
            }

            foreach (Ability a in options.Where(a => !others.Any(w => Reached(w, a).Contains(i))))
            {
                problems.Add($"{room}: exit {x.Name} needs {x.Requires}, but {Describe(a)} reaches it from no way into the room");
            }

            foreach (int w in others.Where(w => ways[w].Base && Reached(w, Ability.None).Contains(i)))
            {
                problems.Add($"{room}: exit {x.Name} needs {x.Requires}, but the base moveset reaches it {ways[w].What}");
            }
        }

        return problems;
    }

    /// <summary>
    /// The map with the rooms' insides (<see cref="MapSolver.SolveInside"/>): each room walked by the planner from the way the pilgrim
    /// came in, with the abilities it owns, taking the relics it reaches there (then walking again with them), leaving only through
    /// the exits it reaches. From waking in the start room, no reachable (room, way in, abilities) may be a softlock, the goal must be
    /// reached (its goal area walked into), and every relic taken. Returns the summary.
    /// </summary>
    private string CheckInside()
    {
        var reaches = new ConcurrentDictionary<WayState, RoomReach>();
        InsideReport report = MapSolver.SolveInside(Graph, state => reaches[state] = Inside(state, clumsy: false));
        CheckSlack(reaches);

        foreach (MapItem item in report.UntakenItems)
        {
            _problems.Add($"map inside the rooms: the {item.Item} in {item.Room} is reached from no way into the room");
        }

        foreach (MapRelic item in report.UntakenRelics)
        {
            _problems.Add($"map inside the rooms: the {item.Ability} relic in {item.Room} is reached from no way into the room");
        }

        foreach ((WayState state, IReadOnlyList<WayState> path) in report.Softlocks.Take(5))
        {
            _problems.Add($"map inside the rooms: softlock in {state}: no goal can be reached from there; reached by {string.Join(" → ", path)}");
        }

        if (!report.GoalReachable)
        {
            _problems.Add("map inside the rooms: no goal is reached walking the rooms from where the pilgrim comes in" +
                          (report.UnenteredRooms.Count > 0 ? $"; never entered: {string.Join(", ", report.UnenteredRooms)}" : ""));
        }

        return $"INSIDE OK: {report.ReachableStates} states (room, way in, abilities), every room walked from each way in, no softlock; " +
               "a clumsier pilgrim reaches all the same";
    }

    /// <summary>What the planner reaches in a room from the way in of <paramref name="state"/>, taking the relics it reaches there.</summary>
    private RoomReach Inside(WayState state, bool clumsy)
    {
        RoomShape shape = _shapes[state.Room];
        WayIn way = state.Entry == ""
            ? new WayIn("waking", shape.Spawn, Vector2.Zero, 0, -1, true, Ability.None)
            : Arriving(shape, shape.Exits.FindIndex(x => x.Name == state.Entry), Requirement.None);
        int relics = shape.Exits.Count, items = relics + shape.Relics.Count, goals = items + shape.Items.Count;
        Ability owned = state.Owned;
        while (true)
        {
            HashSet<int> reached = Reached(state.Room, way, owned, clumsy);
            Ability more = Enumerable.Range(0, shape.Relics.Count).Where(r => reached.Contains(relics + r))
                .Aggregate(owned, (o, r) => o | shape.Relics[r].Ability);
            if (more != owned)
            {
                owned = more; // walked again with what the relic gives (the planner starts over from the way in: never less)
                continue;
            }

            // Items change no walk (a door is opened at its exit), so they are taken once the walk is done.
            ItemSet held = Enumerable.Range(0, shape.Items.Count).Where(i => reached.Contains(items + i))
                .Aggregate(state.Held, (h, i) => h.With(shape.Items[i].Item));
            var exits = shape.Exits.Where((x, i) => !x.Sealed && reached.Contains(i)).Select(x => x.Name).ToList();
            return new RoomReach(owned, held, exits, Enumerable.Range(goals, shape.Goals.Count).Any(reached.Contains));
        }
    }

    /// <summary>
    /// Room for error: every state the inside check walked is walked again by a clumsier pilgrim (<see cref="MoveLimits.Clumsy"/>:
    /// jumps 18% lower, dashes 35% shorter, runs 20% slower, never takes off from the very end of a ledge, no ledge assist), which must
    /// reach every way out, relic and goal the exact one does. A way only perfect play takes is a problem, once per room, way in and
    /// way out (with the fewest abilities). Before this held (2026-11-01), ladders of 3 m steps against a 3.2 m jump made the second
    /// room a trial.
    /// </summary>
    private void CheckSlack(ConcurrentDictionary<WayState, RoomReach> reaches)
    {
        var states = reaches.Keys.ToList();
        var clumsy = new RoomReach[states.Count];
        Parallel.For(0, states.Count, i => clumsy[i] = Inside(states[i], clumsy: true));
        var seen = new HashSet<(string, string, string)>();
        var lines = new List<string>();
        foreach (int i in Enumerable.Range(0, states.Count).OrderBy(i => BitOperations.PopCount((uint)states[i].Owned)))
        {
            WayState s = states[i];
            RoomReach exact = reaches[s], loose = clumsy[i];
            var missed = exact.Exits.Except(loose.Exits).Select(x => $"exit {x}").ToList();
            Ability lost = exact.Owned & ~loose.Owned;
            if (lost != Ability.None)
            {
                missed.Add($"the {lost} relic");
            }

            foreach (string item in exact.Held.Ids.Where(i => !loose.Held.Contains(i)))
            {
                missed.Add($"the {item}");
            }

            if (exact.Goal && !loose.Goal)
            {
                missed.Add("the goal");
            }

            foreach (string m in missed.Where(m => seen.Add((s.Room, s.Entry, m))))
            {
                lines.Add($"{s} : only near-perfect play reaches {m} (a clumsier pilgrim does not; tools: --reach-report {s.Room} --slack-report)");
            }
        }

        _problems.AddRange(lines.Order(StringComparer.Ordinal).Select(l => $"no room for error: {l}"));
    }
}
