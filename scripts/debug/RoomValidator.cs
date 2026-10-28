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
    private sealed record RoomShape(List<Box> Solids, List<ExitShape> Exits, List<System.Numerics.Vector2> Wakes);

    private sealed record ExitShape(string Name, System.Numerics.Vector2 Position, System.Numerics.Vector2 Size, string Requires, int ArrivalSide);
    private bool _done;

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

        foreach (RoomExit exit in exits.Where(e => !e.Open && Requirement.TryParse(e.Requires, out _, out _)))
        {
            _sealed.Add((name, exit.Requirement));
        }

        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            _graph.AddItem(new MapItem(name, relic.Name, relic.Ability));
        }

        var solids = room.Blocks().Select(b => Box.Around(N(new Vector2(b.GlobalPosition.X, b.GlobalPosition.Y)), N(new Vector2(b.Size.X, b.Size.Y)))).ToList();
        var exitShapes = exits.Where(e => e.TargetRoom != "")
            .Select(e => new ExitShape(e.Name, N(e.RoomPosition), N(e.Size), e.Requires, e.ArrivalSide)).ToList();
        var wakes = room.All<Shrine>().Select(s => N(new Vector2(s.GlobalPosition.X, s.GlobalPosition.Y))).ToList();
        if (World.RoomPath(name) == StartRoom)
        {
            wakes.Add(N(new Vector2(room.SpawnPoint.GlobalPosition.X, room.SpawnPoint.GlobalPosition.Y)));
        }

        _shapes[name] = new RoomShape(solids, exitShapes, wakes);

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
        text.Append($"MAP OK: {_graph.Rooms.Count} rooms, {_graph.Edges.Count} exits, {_graph.Items.Count} relics, ")
            .Append($"{report.ReachableStates} states, no softlock, ")
            .Append(report.AbilityCount == 0 ? "no abilities on the map" : $"{report.Orders.Count} of {report.PossibleOrders} ability orders");
        foreach ((Ability ability, SortedSet<int> at) in report.Positions)
        {
            text.Append($"; {ability} at {string.Join("/", at.Select(i => i + 1))}");
        }

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

    /// <summary>
    /// The geometry sanity check (M7): the base moveset, simulated with the live tuning (<see cref="ReachPlanner"/>), must reach
    /// every exit marked for the base moveset from some way into its room, and must not reach an exit that needs an ability
    /// from a way in that needed none (that requirement would be a lie, or the geometry drifted from it).
    /// </summary>
    private void CheckGeometry()
    {
        var tuning = GD.Load<PlayerTuning>("res://resources/player_tuning.tres");
        var limits = new MoveLimits(tuning.RunSpeed, tuning.AirAcceleration, tuning.AirDeceleration, tuning.JumpHeight, tuning.JumpTimeToApex,
            tuning.JumpCutFactor, tuning.FallGravityMultiplier, tuning.ApexHangThreshold, tuning.ApexGravityMultiplier, tuning.MaxFallSpeed,
            tuning.DashDistance, tuning.DashDuration, tuning.DashExitSpeedFactor, tuning.AirDashes);
        float rise = 0.85f * tuning.JumpTakeOffSpeed;
        foreach ((string room, RoomShape shape) in _shapes.OrderBy(r => r.Key, System.StringComparer.Ordinal))
        {
            var planner = new ReachPlanner(limits, shape.Solids);
            var triggers = shape.Exits.Select(e => Box.Around(e.Position, e.Size)).ToList();

            // Ways in: arriving through an exit (by the edge leading here: its requirement is what the pilgrim has at least), or waking.
            var entries = new List<(string What, System.Numerics.Vector2 At, System.Numerics.Vector2 Velocity, int Steer, bool Base, int Exit)>();
            foreach (MapEdge edge in _graph.Edges.Where(e => e.To == room))
            {
                int index = shape.Exits.FindIndex(x => x.Name == edge.ToExit);
                if (index < 0)
                {
                    continue;
                }

                ExitShape x = shape.Exits[index];
                bool vertical = ExitMath.IsVertical(x.Size);
                var velocity = !vertical ? System.Numerics.Vector2.Zero
                    : x.ArrivalSide != 0 ? new System.Numerics.Vector2(0f, rise) : new System.Numerics.Vector2(0f, -5f);
                entries.Add(($"arriving through {x.Name}", x.Position, velocity, vertical ? x.ArrivalSide : 0,
                    edge.Requires.IsMetBy(Ability.None), index));
            }

            foreach (System.Numerics.Vector2 wake in shape.Wakes)
            {
                entries.Add(($"waking at ({wake.X:0.#}, {wake.Y:0.#})", wake, System.Numerics.Vector2.Zero, 0, true, -1));
            }

            var reachedFrom = entries.Select(e => planner.Reach(e.At, e.Velocity, triggers, e.Steer).Triggers).ToList();
            for (int i = 0; i < shape.Exits.Count; i++)
            {
                ExitShape x = shape.Exits[i];
                bool baseExit = Requirement.TryParse(x.Requires, out Requirement r, out _) && r.IsMetBy(Ability.None);
                if (baseExit && entries.Count > 0 && !Enumerable.Range(0, entries.Count).Any(e => entries[e].Exit != i && reachedFrom[e].Contains(i))
                    && entries.Any(e => e.Exit != i))
                {
                    _problems.Add($"{room}: exit {x.Name} needs nothing, but the base moveset reaches it from no way into the room");
                }

                if (!baseExit)
                {
                    foreach (int e in Enumerable.Range(0, entries.Count).Where(e => entries[e].Base && entries[e].Exit != i && reachedFrom[e].Contains(i)))
                    {
                        _problems.Add($"{room}: exit {x.Name} needs {x.Requires}, but the base moveset reaches it {entries[e].What}");
                    }
                }
            }
        }
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
