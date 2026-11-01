using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Checks every room scene in <see cref="World.RoomFolder"/> and quits (`-- --validate-rooms`, run by
/// tools/check.sh). Prints `ROOMS OK` or one error per problem:
/// - the spawn point and every exit lie inside the camera bounds;
/// - gameplay blocks are axis-aligned with their edges on the block grid, kit pieces have their origin on the piece grid;
/// - exits have unique names, a valid requirement, an existing target room and exit, and a target that leads
///   back unless they are one-way;
/// - the room has at least one photo spot.
/// Then the map (M7, docs/long-term.md objective 2), extracted from the rooms in the game (<see cref="Room.InGame"/>):
/// - the rooms placed from their exits line up around every loop and don't overlap (<see cref="MapLayout"/>);
/// - every room is reachable from the start, a goal (<see cref="MapGoal"/>) is, and no reachable state is a
///   softlock (<see cref="MapSolver"/>); prints the achievable ability orders (`--map-report`: the whole graph).
/// </summary>
public partial class RoomValidator : Node
{
    /// <summary>Two rooms' bounds may overlap this much on the map (their walls at a shared doorway), meters.</summary>
    private const float DoorwayOverlap = 6f;

    /// <summary>The whole game's draft map (M7), with design notes around its `map` blocks.</summary>
    private const string DraftPath = "res://docs/map-draft.md";

    private readonly List<string> _problems = new();
    private readonly MapGraph _graph = new();
    private readonly List<(string Room, Requirement Requires)> _sealed = new();
    private readonly List<MapLink> _links = new();
    private readonly Dictionary<string, (System.Numerics.Vector2, System.Numerics.Vector2)> _bounds = new();
    private readonly Dictionary<string, RoomShape> _shapes = new();

    /// <summary>What the geometry check needs of a room: its solids, its open exits, and where the pilgrim can wake.</summary>
    private sealed record RoomShape(List<Box> Solids, List<ExitShape> Exits, List<System.Numerics.Vector2> Wakes, List<System.Numerics.Vector2> Anchors,
        List<LiftPath> Lifts, List<Box> Sound, List<Box> Waters, List<Box> Heat, List<(Box Area, Ability Ability)> Relics, List<Box> Goals, List<Box> Metal,
        System.Numerics.Vector2 Spawn);

    private sealed record ExitShape(string Name, System.Numerics.Vector2 Position, System.Numerics.Vector2 Size, string Requires, int ArrivalSide, bool Sealed);
    private bool _done;

    /// <summary>The abilities the geometry check simulates (all five).</summary>
    private const Ability Simulated = Ability.MagGrip | Ability.Harpoon | Ability.Drill | Ability.Ballast | Ability.HeatPlating;

    /// <summary>Each simulated ability on its own.</summary>
    private static readonly Ability[] Singles = { Ability.MagGrip, Ability.Harpoon, Ability.Drill, Ability.Ballast, Ability.HeatPlating };

    /// <summary>The simulated abilities taken together, fewest first, for ways that need more than one.</summary>
    private static readonly Ability[] Combined = Enumerable.Range(1, (int)Simulated)
        .Select(i => (Ability)i).Where(a => (a & ~Simulated) == 0 && System.Numerics.BitOperations.PopCount((uint)a) > 1)
        .OrderBy(a => System.Numerics.BitOperations.PopCount((uint)a)).ToArray();

    /// <summary>A room whose ways in print what they reach (`--reach-report &lt;Room&gt;`), to debug the geometry check.</summary>
    public string ReachReport { get; set; } = "";

    /// <summary>With `--reach-report`, the abilities every way in is walked with (`--abilities`), instead of what the way guarantees.</summary>
    public Ability? ReachWith { get; set; }

    /// <summary>With `--reach-report`, print the clumsier pilgrim's reach (`--slack-report`).</summary>
    public bool SlackReport { get; set; }

    /// <summary>The start room's scene path (the world's start room).</summary>
    public string StartRoom { get; set; } = "";

    /// <summary>Print the whole map graph and every achievable order.</summary>
    public bool Report { get; set; }

    public override void _Process(double delta)
    {
        if (_done)
        {
            return;
        }

        _done = true;
        int rooms = 0, exits = 0;
        foreach (string path in RoomScenes())
        {
            if (GD.Load<PackedScene>(path).Instantiate() is not Room room)
            {
                _problems.Add($"{path}: the root node is not a Room");
                continue;
            }

            AddChild(room);
            rooms++;
            exits += Check(room, path);
            RemoveChild(room);
            room.Free();

            // Let the freed room's C# wrappers finalize now, while nothing else touches the engine: left to the
            // finalizer thread during the next room's setup, they intermittently crashed Godot (gchandle.is_released).
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        string map = CheckMap() + "\n" + CheckDraft();
        foreach (string problem in _problems)
        {
            GD.PushError($"ROOM CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"ROOMS OK: {rooms} rooms, {exits} exits");
            GD.Print(map);
        }
        else if (Report)
        {
            GD.Print(map); // the placements help find what doesn't line up
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }

    private static IEnumerable<string> RoomScenes() =>
        DirAccess.GetFilesAt(World.RoomFolder).Where(f => f.EndsWith(".tscn")).Order().Select(f => $"{World.RoomFolder}/{f}");

    /// <summary>Checks one room (in the tree at the origin, so global positions are room positions); returns its exit count.</summary>
    private int Check(Room room, string path)
    {
        void Problem(string what) => _problems.Add($"{path}: {what}");
        bool Inside(Vector3 p) => p.X >= room.BoundsMin.X && p.X <= room.BoundsMax.X && p.Y >= room.BoundsMin.Y && p.Y <= room.BoundsMax.Y;

        if (!Inside(room.SpawnPoint.GlobalPosition))
        {
            Problem($"spawn point {room.SpawnPoint.GlobalPosition} is outside the camera bounds");
        }

        List<Node> nodes = Descendants(room).Where(n => n.Owner == room).ToList();
        foreach (Block block in nodes.OfType<Block>())
        {
            Vector3 p = block.GlobalPosition;
            if (!block.GlobalBasis.IsEqualApprox(Basis.Identity))
            {
                Problem($"block {block.Name} is rotated; gameplay blocks stay axis-aligned");
            }

            if (!GridSnap.EdgesOnGrid(p.X, block.Size.X, GridSnap.BlockStep) || !GridSnap.EdgesOnGrid(p.Y, block.Size.Y, GridSnap.BlockStep))
            {
                Problem($"block {block.Name} at {p} size {block.Size} has edges off the {GridSnap.BlockStep} m grid");
            }
        }

        foreach (MeshInstance3D piece in nodes.OfType<MeshInstance3D>().Where(m => m.Mesh is KitMesh))
        {
            Vector3 p = piece.GlobalPosition;
            if (!GridSnap.OnGrid(p.X, GridSnap.PieceStep) || !GridSnap.OnGrid(p.Y, GridSnap.PieceStep) || !GridSnap.OnGrid(p.Z, GridSnap.PieceStep))
            {
                Problem($"kit piece {piece.Name} at {p} is off the {GridSnap.PieceStep} m grid");
            }
        }

        List<RoomExit> exits = nodes.OfType<RoomExit>().ToList();
        foreach (IGrouping<string, RoomExit> same in exits.GroupBy(e => e.Name.ToString()).Where(g => g.Count() > 1))
        {
            Problem($"{same.Count()} exits are named {same.Key}");
        }

        foreach (RoomExit exit in exits)
        {
            CheckExit(exit, path, Inside(exit.GlobalPosition), Problem);
        }

        if (!room.PhotoSpots().Any())
        {
            Problem("no PhotoSpot (tools/capture.sh needs at least one)");
        }

        if (room.InGame)
        {
            Extract(room, World.NameOf(path), exits);
        }

        return exits.Count;
    }

    /// <summary>The room's part of the map graph: its exits (one edge each, for leaving through it), relics and goals.</summary>
    private void Extract(Room room, string name, List<RoomExit> exits)
    {
        static System.Numerics.Vector2 N(Vector2 v) => new(v.X, v.Y);
        _graph.AddRoom(name);
        _bounds[name] = (N(room.BoundsMin), N(room.BoundsMax));
        foreach (RoomExit exit in exits.Where(e => e.TargetRoom != "" && ResourceLoader.Exists(e.TargetRoom)))
        {
            string target = World.NameOf(exit.TargetRoom);
            if (!Requirement.TryParse(exit.Requires, out Requirement requires, out _))
            {
                continue;
            }

            _graph.AddEdge(new MapEdge(name, exit.Name, target, exit.TargetExit, requires));
            Vector2? arrival = ArrivalPosition(exit.TargetRoom, exit.TargetExit);
            if (arrival is { } to)
            {
                _links.Add(new MapLink(name, exit.Name, N(exit.RoomPosition), target, exit.TargetExit, N(to)));
            }
        }

        foreach (RoomExit exit in exits.Where(e => e.Sealed && Requirement.TryParse(e.Requires, out _, out _)))
        {
            _sealed.Add((name, exit.Requirement));
        }

        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            _graph.AddItem(new MapItem(name, relic.Name, relic.Ability));
        }

        static Box BoxOf(Block b) => Box.Around(N(new Vector2(b.GlobalPosition.X, b.GlobalPosition.Y)), N(new Vector2(b.Size.X, b.Size.Y)));
        var solids = room.Blocks().Select(BoxOf).ToList();
        var sound = room.Blocks().Where(b => !b.Cracked).Select(BoxOf).ToList();
        var exitShapes = exits.Where(e => e.TargetRoom == "" || ResourceLoader.Exists(e.TargetRoom))
            .Select(e => new ExitShape(e.Name, N(e.RoomPosition), N(e.Size), e.Requires, e.ArrivalSide, e.TargetRoom == "")).ToList();
        var wakes = room.All<Shrine>().Select(s => N(new Vector2(s.GlobalPosition.X, s.GlobalPosition.Y))).ToList();
        if (World.RoomPath(name) == StartRoom)
        {
            wakes.Add(N(new Vector2(room.SpawnPoint.GlobalPosition.X, room.SpawnPoint.GlobalPosition.Y)));
        }

        var anchors = room.All<HarpoonAnchor>().Select(a => N(new Vector2(a.GlobalPosition.X, a.GlobalPosition.Y))).ToList();
        var waters = room.All<WaterBody>().Where(w => w.Swim).Select(w => w.PlayRegion).ToList();
        var heat = room.All<HeatZone>().Select(z => z.Region).ToList();
        // Where walking takes a relic (its range from the feet) and reaches a goal (the feet inside its area), as trigger boxes.
        var relics = room.All<AbilityRelic>().Select(r => (Box.Around(N(new Vector2(r.GlobalPosition.X, r.GlobalPosition.Y + 0.1f)),
            new System.Numerics.Vector2(1.6f, 0.4f)), r.Ability)).ToList();
        var goals = room.All<MapGoal>().Where(g => g.Size != Vector2.Zero)
            .Select(g => Box.Around(N(new Vector2(g.GlobalPosition.X, g.GlobalPosition.Y)), N(g.Size) + new System.Numerics.Vector2(0f, 0f))).ToList();
        var metal = room.Blocks().Where(b => b.Surface == SurfaceKind.Metal && !b.Cracked).Select(BoxOf).ToList();
        _shapes[name] = new RoomShape(solids, exitShapes, wakes, anchors, room.All<Lift>().Select(l => l.Path).ToList(), sound, waters, heat,
            relics, goals, metal, N(new Vector2(room.SpawnPoint.GlobalPosition.X, room.SpawnPoint.GlobalPosition.Y)));

        if (room.All<MapGoal>().Any())
        {
            _graph.AddGoal(name);
        }
    }

    /// <summary>Where an exit sits in its room (the scene instanced but not run).</summary>
    private static Vector2? ArrivalPosition(string scene, string exitName)
    {
        Node target = GD.Load<PackedScene>(scene).Instantiate();
        try
        {
            if (target.FindChild(exitName, recursive: true, owned: true) is not RoomExit arrival)
            {
                return null;
            }

            Vector3 p = Vector3.Zero;
            for (Node3D? n = arrival; n is not null && n != target; n = n.GetParent() as Node3D)
            {
                p = n.Transform * p;
            }

            return new Vector2(p.X, p.Y);
        }
        finally
        {
            target.Free();
        }
    }

    /// <summary>Solves the extracted map and lays it out; adds problems; returns the summary line(s).</summary>
    private string CheckMap()
    {
        string start = World.NameOf(StartRoom);
        if (!_graph.Rooms.Contains(start))
        {
            _problems.Add($"map: the start room {start} is not a room in the game");
            return "";
        }

        _graph.Start = start;
        if (_graph.Goals.Count == 0)
        {
            _problems.Add("map: no room holds a MapGoal (where the journey ends)");
        }

        MapLayout.Result layout = MapLayout.Place(start, _links, _bounds, DoorwayOverlap);
        foreach (string problem in layout.Problems)
        {
            _problems.Add($"map layout: {problem}");
        }

        CheckGeometry();
        string inside = CheckInside();
        MapReport report = MapSolver.Solve(_graph);
        foreach (string room in report.UnreachableRooms)
        {
            _problems.Add($"map: {room} can't be reached from {start}");
        }

        if (!report.GoalReachable)
        {
            _problems.Add($"map: no goal can be reached from {start}");
        }

        foreach ((SolverState state, IReadOnlyList<SolverState> path) in report.Softlocks.Take(5))
        {
            _problems.Add($"map: softlock at {state}: no goal can be reached from there; reached by {string.Join(" → ", path)}");
        }

        if (report.AbilityCount == 5 && report.Orders.Count < MapDraft.OrderTarget)
        {
            _problems.Add($"map: only {report.Orders.Count} of {report.PossibleOrders} ability orders are achievable (target {MapDraft.OrderTarget})");
        }

        var text = new System.Text.StringBuilder();
        if (Report && _problems.Count > 0)
        {
            foreach ((string room, System.Numerics.Vector2 offset) in layout.Offsets.OrderBy(o => o.Key, System.StringComparer.Ordinal))
            {
                text.Append($"\n  {room} at ({offset.X:0.##}, {offset.Y:0.##})");
            }

            return text.ToString();
        }

        text.Append($"MAP OK: {_graph.Rooms.Count} rooms, {_graph.Edges.Count} exits, {_graph.Items.Count} relics, ")
            .Append($"{report.ReachableStates} states, no softlock, ")
            .Append(report.AbilityCount == 0 ? "no abilities on the map" : $"{report.Orders.Count} of {report.PossibleOrders} ability orders");
        foreach ((Ability ability, SortedSet<int> at) in report.Positions)
        {
            text.Append($"; {ability} at {string.Join("/", at.Select(i => i + 1))}");
        }

        text.Append('\n').Append(inside);

        if (Report)
        {
            text.Append("\nmap graph (leaving through an exit needs its requirement):");
            foreach (MapEdge e in _graph.Edges)
            {
                string needs = e.Requires.ToString();
                text.Append($"\n  {e.From}:{e.FromExit} → {e.To}:{e.ToExit}{(needs == "" ? "" : $"  needs {needs}")}");
            }

            foreach (MapItem item in _graph.Items)
            {
                text.Append($"\n  relic {item.Ability} in {item.Room} ({item.Name})");
            }

            foreach ((string room, System.Numerics.Vector2 offset) in layout.Offsets.OrderBy(o => o.Key, System.StringComparer.Ordinal))
            {
                text.Append($"\n  {room} at ({offset.X:0.##}, {offset.Y:0.##})");
            }

            foreach (Ability[] order in report.Orders)
            {
                text.Append($"\n  order: {string.Join(" → ", order)}");
            }
        }

        return text.ToString();
    }

    /// <summary>The whole game's draft: its targets (<see cref="MapDraft.Check"/>) and its agreement with the built rooms; returns the summary line(s).</summary>
    private string CheckDraft()
    {
        MapDraft draft;
        try
        {
            draft = MapDraft.FromMarkdown(FileAccess.GetFileAsString(DraftPath));
        }
        catch (System.FormatException e)
        {
            _problems.Add($"map draft ({DraftPath}): {e.Message}");
            return "";
        }

        (MapReport report, List<string> problems) = draft.Check();
        _problems.AddRange(problems.Select(p => $"map draft: {p}"));
        _problems.AddRange(draft.Differences(_graph, _sealed).Select(p => $"map draft differs from the rooms: {p}"));

        var text = new System.Text.StringBuilder($"DRAFT OK: {draft.Summary(report)}");
        if (Report)
        {
            text.Append("\ndraft ways:");
            foreach (MapEdge e in draft.Graph.Edges)
            {
                string needs = e.Requires.ToString();
                text.Append($"\n  {e.From} → {e.To}{(needs == "" ? "" : $"  needs {needs}")}");
            }

            foreach (Ability[] order in report.Orders)
            {
                text.Append($"\n  draft order: {string.Join(" → ", order)}");
            }
        }

        return text.ToString();
    }

    /// <summary>The planner numbers, from the live tuning (the single source).</summary>
    private (MoveLimits Move, HarpoonLimits Harpoon, GripLimits Grip, WaterLimits Water, float Rise)? _tuning;

    private (MoveLimits Move, HarpoonLimits Harpoon, GripLimits Grip, WaterLimits Water, float Rise) Tuning()
    {
        if (_tuning is { } t)
        {
            return t;
        }

        var tuning = GD.Load<PlayerTuning>("res://resources/player_tuning.tres");
        var move = new MoveLimits(tuning.RunSpeed, tuning.AirAcceleration, tuning.AirDeceleration, tuning.JumpHeight, tuning.JumpTimeToApex,
            tuning.JumpCutFactor, tuning.FallGravityMultiplier, tuning.ApexHangThreshold, tuning.ApexGravityMultiplier, tuning.MaxFallSpeed,
            tuning.DashDistance, tuning.DashDuration, tuning.DashExitSpeedFactor, tuning.AirDashes, LedgeAssist: tuning.LedgeAssistHeight);
        var harpoon = new HarpoonLimits(tuning.HarpoonRange, tuning.HarpoonMinDistance, tuning.HarpoonZipSpeed, tuning.HarpoonZipAcceleration,
            tuning.HarpoonExitSpeedFactor, tuning.HarpoonExitLift);
        var grip = new GripLimits(tuning.GripLeapAwaySpeed, tuning.GripLeapUpFactor, tuning.GripRegrabDelay);
        _tuning = t = (move, harpoon, grip, tuning.Water, 0.85f * tuning.JumpTakeOffSpeed);
        return t;
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Room, Ability With, bool Clumsy), ReachPlanner> _planners = new();

    /// <summary>
    /// The planner for a room with some abilities (cached): Mag-grip walks its metal, the Harpoon zips between its anchors, the Drill
    /// bores what it reaches (the cracked blocks gone), Ballast sinks and rises in its water, Heat plating walks through its heat.
    /// </summary>
    private ReachPlanner Planner(string room, Ability with, bool clumsy = false) => _planners.GetOrAdd((room, with & Simulated, clumsy), key =>
    {
        RoomShape shape = _shapes[key.Room];
        Ability g = key.With;
        var t = Tuning();
        bool grip = (g & Ability.MagGrip) != 0;
        return new ReachPlanner(key.Clumsy ? t.Move.Clumsy() : t.Move, (g & Ability.Drill) != 0 ? shape.Sound : shape.Solids, (g & Ability.Harpoon) != 0 ? t.Harpoon : null,
            shape.Anchors, shape.Lifts, t.Water, shape.Waters, ballast: (g & Ability.Ballast) != 0,
            hazards: (g & Ability.HeatPlating) != 0 ? null : shape.Heat, grip: grip ? t.Grip : null, metal: grip ? shape.Metal : null);
    });

    /// <summary>
    /// A way into a room: arriving through an exit (<see cref="Exit"/>, its index) by an edge needing <see cref="Requires"/>, or waking
    /// (Exit −1); where and how the body enters, and the abilities every option of the requirement holds (<see cref="Guaranteed"/>:
    /// whoever arrives through a way only the Drill opens has the Drill, and bores what it reaches there).
    /// </summary>
    private sealed record WayIn(string What, System.Numerics.Vector2 At, System.Numerics.Vector2 Velocity, int Steer, int Exit, bool Base,
        Ability Guaranteed);

    private WayIn Arriving(RoomShape shape, int index, Requirement requires)
    {
        ExitShape x = shape.Exits[index];
        bool vertical = ExitMath.IsVertical(x.Size);
        var velocity = !vertical ? System.Numerics.Vector2.Zero
            : x.ArrivalSide != 0 ? new System.Numerics.Vector2(0f, Tuning().Rise) : new System.Numerics.Vector2(0f, -5f);
        return new WayIn($"arriving through {x.Name}", x.Position, velocity, vertical ? x.ArrivalSide : 0, index, requires.IsMetBy(Ability.None),
            requires.Options.Aggregate(Simulated, (all, option) => all & option));
    }

    private List<WayIn> WaysIn(string room)
    {
        RoomShape shape = _shapes[room];
        var ways = new List<WayIn>();
        foreach (MapEdge edge in _graph.Edges.Where(e => e.To == room))
        {
            int index = shape.Exits.FindIndex(x => x.Name == edge.ToExit);
            if (index >= 0)
            {
                ways.Add(Arriving(shape, index, edge.Requires));
            }
        }

        foreach (System.Numerics.Vector2 wake in shape.Wakes)
        {
            ways.Add(new WayIn($"waking at ({wake.X:0.#}, {wake.Y:0.#})", wake, System.Numerics.Vector2.Zero, 0, -1, true, Ability.None));
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
    /// The geometry sanity check (M7): the base moveset, simulated with the live tuning (<see cref="ReachPlanner"/>), must reach every
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
        foreach ((string room, RoomShape shape) in _shapes.OrderBy(r => r.Key, System.StringComparer.Ordinal))
        {
            var triggers = shape.Exits.Select(e => Box.Around(e.Position, e.Size)).ToList();
            List<WayIn> ways = WaysIn(room);
            var memo = new Dictionary<(int, Ability), HashSet<int>>();
            HashSet<int> Reached(int w, Ability with)
            {
                with = (with | ways[w].Guaranteed) & Simulated;
                return memo.TryGetValue((w, with), out HashSet<int>? r) ? r
                    : memo[(w, with)] = Planner(room, with).Reach(ways[w].At, ways[w].Velocity, triggers, ways[w].Steer).Triggers;
            }

            if (ReachReport == room)
            {
                // `--reach-report <Room>`: the ledges each way in reaches, to see where a planner stops (with `--slack-report`, the clumsy pilgrim's).
                for (int w = 0; w < ways.Count; w++)
                {
                    ReachPlanner p = Planner(room, ReachWith ?? ways[w].Guaranteed, clumsy: SlackReport);
                    var (exits, ledges) = p.Reach(ways[w].At, ways[w].Velocity, triggers, ways[w].Steer);
                    GD.Print($"REACH {room} {ways[w].What} [{ReachWith ?? ways[w].Guaranteed}]: exits " +
                             string.Join(", ", exits.Select(t => shape.Exits[t].Name)) + "; ledges " +
                             string.Join(" ", ledges.OrderBy(l => p.Ledges[l].Y).Select(l => $"{(p.Ledges[l].Swim ? "~" : "")}{p.Ledges[l].Y:0.##}@{p.Ledges[l].MinX:0.##}..{p.Ledges[l].MaxX:0.##}")));
                }

                ReachPlanner none = Planner(room, Ability.None);
                GD.Print($"REACH {room} all ledges: " + string.Join(" ", none.Ledges.OrderBy(l => l.Y).Select(l => $"{(l.Swim ? "~" : "")}{l.Y:0.##}@{l.MinX:0.##}..{l.MaxX:0.##}")));
            }

            for (int i = 0; i < shape.Exits.Count; i++)
            {
                ExitShape x = shape.Exits[i];
                var others = Enumerable.Range(0, ways.Count).Where(w => ways[w].Exit != i).ToList();
                if (others.Count == 0 || !Requirement.TryParse(x.Requires, out Requirement r, out _))
                {
                    continue;
                }

                if (r.IsMetBy(Ability.None))
                {
                    if (!x.Sealed && !others.Any(w => Reached(w, Ability.None).Contains(i)))
                    {
                        _problems.Add($"{room}: exit {x.Name} needs nothing, but the base moveset reaches it from no way into the room");
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
                    _problems.Add($"{room}: exit {x.Name} needs {x.Requires}, but {Describe(a)} reaches it from no way into the room");
                }

                foreach (int w in others.Where(w => ways[w].Base && Reached(w, Ability.None).Contains(i)))
                {
                    _problems.Add($"{room}: exit {x.Name} needs {x.Requires}, but the base moveset reaches it {ways[w].What}");
                }
            }
        }
    }

    /// <summary>
    /// The map with the rooms' insides (<see cref="MapSolver.SolveInside"/>): each room walked by the planner from the way the pilgrim
    /// came in, with the abilities it owns, taking the relics it reaches there (then walking again with them), leaving only through
    /// the exits it reaches. From waking in the start room, no reachable (room, way in, abilities) may be a softlock, the goal must be
    /// reached (its <see cref="MapGoal"/> area walked into), and every relic taken. Returns the summary.
    /// </summary>
    private string CheckInside()
    {
        var reaches = new System.Collections.Concurrent.ConcurrentDictionary<WayState, RoomReach>();
        InsideReport report = MapSolver.SolveInside(_graph, state => reaches[state] = Inside(state, clumsy: false));
        CheckSlack(reaches);

        foreach (MapItem item in report.UntakenItems)
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
            ? new WayIn("waking", shape.Spawn, System.Numerics.Vector2.Zero, 0, -1, true, Ability.None)
            : Arriving(shape, shape.Exits.FindIndex(x => x.Name == state.Entry), Requirement.None);
        int relics = shape.Exits.Count, goals = relics + shape.Relics.Count;
        var triggers = shape.Exits.Select(e => Box.Around(e.Position, e.Size)).Concat(shape.Relics.Select(r => r.Area)).Concat(shape.Goals).ToList();
        Ability owned = state.Owned;
        while (true)
        {
            HashSet<int> reached = Planner(state.Room, owned, clumsy).Reach(way.At, way.Velocity, triggers, way.Steer).Triggers;
            Ability more = Enumerable.Range(0, shape.Relics.Count).Where(r => reached.Contains(relics + r))
                .Aggregate(owned, (o, r) => o | shape.Relics[r].Ability);
            if (more != owned)
            {
                owned = more; // walked again with what the relic gives (the planner starts over from the way in: never less)
                continue;
            }

            var exits = shape.Exits.Where((x, i) => !x.Sealed && reached.Contains(i)).Select(x => x.Name).ToList();
            return new RoomReach(owned, exits, Enumerable.Range(goals, shape.Goals.Count).Any(reached.Contains));
        }
    }

    /// <summary>
    /// Room for error: every state the inside check walked is walked again by a clumsier pilgrim (<see cref="MoveLimits.Clumsy"/>:
    /// jumps 18% lower, dashes 35% shorter, runs 20% slower, never takes off from the very end of a ledge, no ledge assist), which must reach every way
    /// out, relic and goal the exact one does. A way only perfect play takes is a problem, once per room, way in and way out (with the
    /// fewest abilities). Before this held (2026-11-01), ladders of 3 m steps against a 3.2 m jump made the second room a trial.
    /// </summary>
    private void CheckSlack(System.Collections.Concurrent.ConcurrentDictionary<WayState, RoomReach> reaches)
    {
        var states = reaches.Keys.ToList();
        var clumsy = new RoomReach[states.Count];
        System.Threading.Tasks.Parallel.For(0, states.Count, i => clumsy[i] = Inside(states[i], clumsy: true));
        var seen = new HashSet<(string, string, string)>();
        var lines = new List<string>();
        foreach (int i in Enumerable.Range(0, states.Count).OrderBy(i => System.Numerics.BitOperations.PopCount((uint)states[i].Owned)))
        {
            WayState s = states[i];
            RoomReach exact = reaches[s], loose = clumsy[i];
            var missed = exact.Exits.Except(loose.Exits).Select(x => $"exit {x}").ToList();
            Ability lost = exact.Owned & ~loose.Owned;
            if (lost != Ability.None)
            {
                missed.Add($"the {lost} relic");
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

        _problems.AddRange(lines.Order(System.StringComparer.Ordinal).Select(l => $"no room for error: {l}"));
    }

    private static void CheckExit(RoomExit exit, string path, bool inside, System.Action<string> problem)
    {
        string name = exit.Name;
        if (!inside)
        {
            problem($"exit {name} is outside the camera bounds");
        }

        if (!Requirement.TryParse(exit.Requires, out _, out string error))
        {
            problem($"exit {name}: {error}");
        }

        if (exit.ArrivalOnly)
        {
            if (exit.TargetRoom != "" || exit.Requires != "")
            {
                problem($"exit {name} is arrival-only, so it has no target and no requirement");
            }

            return;
        }

        if (exit.TargetRoom == "")
        {
            return; // leads to a room not built yet
        }

        if (!ResourceLoader.Exists(exit.TargetRoom))
        {
            problem($"exit {name} leads to {exit.TargetRoom}, which does not exist");
            return;
        }

        Node target = GD.Load<PackedScene>(exit.TargetRoom).Instantiate();
        try
        {
            if (target.FindChild(exit.TargetExit, recursive: true, owned: true) is not RoomExit arrival)
            {
                problem($"exit {name} leads to {exit.TargetRoom}, which has no RoomExit named '{exit.TargetExit}'");
            }
            else if (!exit.OneWay && (arrival.TargetRoom != path || arrival.TargetExit != name))
            {
                problem($"exit {name} is two-way, but {exit.TargetRoom}:{exit.TargetExit} does not lead back to it (or mark it OneWay)");
            }
        }
        finally
        {
            target.Free();
        }
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (Node nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
