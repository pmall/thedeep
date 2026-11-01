using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Checks every room scene of the world (<see cref="WorldDefinition.RoomsFolder"/>) and quits (`-- --validate-rooms`, run by
/// tools/check.sh). Prints `ROOMS OK` or one error per problem:
/// - the spawn point and every exit lie inside the camera bounds;
/// - gameplay blocks are axis-aligned with their edges on the block grid, kit pieces have their origin on the piece grid
///   (dressing generators only their own: the pieces they build are not owned by the room);
/// - exits have unique names, a valid requirement, an existing target room and exit, and a target that leads
///   back unless they are one-way;
/// - the room has at least one photo spot;
/// - no required export is left empty, in the room's nodes, the bricks it places and the resources they reach, nor in the main
///   scene and its world (<see cref="RequiredExports"/>).
/// Then the map, extracted from the rooms in the game (<see cref="Room.InGame"/>) and proven by the core's <see cref="WorldCheck"/>:
/// layout, reachability, softlocks, ability orders (`--map-report`: the whole graph), the geometry, the map from inside the rooms,
/// room for error; and the world's network (<see cref="MapDraft"/>): its targets, its layers, and its agreement with the built rooms.
/// </summary>
public partial class RoomValidator : Node
{
    private readonly List<string> _problems = new();
    private readonly RequiredExports _exports = new();
    private bool _done;

    /// <summary>The world's network (its `map` blocks), or null if it doesn't parse (a problem then).</summary>
    private MapDraft? _draft;

    /// <summary>The world whose rooms are checked.</summary>
    public required World World { get; init; }

    /// <summary>The numbers the planners simulate (the live tuning, the single source).</summary>
    public required PlayerTuning Tuning { get; init; }

    /// <summary>Upward speed an arrival climbing up through a hatch keeps at least, as a share of a jump's take-off (the room travel's).</summary>
    public required float ClimbRise { get; init; }

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
        _draft = LoadDraft();
        var world = new WorldCheck(new PlannerLimits(Tuning.Move, Tuning.Harpoon, Tuning.Grip, Tuning.Water, ClimbRise * Tuning.JumpTakeOffSpeed));
        CheckMainScene();
        int rooms = 0, exits = 0;
        foreach (string path in World.RoomScenes())
        {
            if (GD.Load<PackedScene>(path).Instantiate() is not Room room)
            {
                _problems.Add($"{path}: the root node is not a Room");
                continue;
            }

            AddChild(room);
            rooms++;
            exits += Check(room, path, world);
            RemoveChild(room);
            room.Free();

            // Let the freed room's C# wrappers finalize now, while nothing else touches the engine: left to the
            // finalizer thread during the next room's setup, they intermittently crashed Godot (gchandle.is_released).
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        string map = world.Check(World.NameOf(StartRoom), _draft?.Targets ?? MapTargets.None, Report);
        _problems.AddRange(world.Problems);
        if (ReachReport != "")
        {
            foreach (string line in world.ReachReport(ReachReport, ReachWith, SlackReport))
            {
                GD.Print(line);
            }
        }

        map += "\n" + CheckDraft(world);
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

    /// <summary>The main scene as authored (not as it runs): its nodes' required exports, and its world's.</summary>
    private void CheckMainScene()
    {
        string path = GetTree().CurrentScene.SceneFilePath;
        Node main = GD.Load<PackedScene>(path).Instantiate();
        try
        {
            _problems.AddRange(_exports.Missing(main).Select(m => $"{path}: {m}"));
        }
        finally
        {
            main.Free();
        }
    }

    /// <summary>Checks one room (in the tree at the origin, so global positions are room positions); returns its exit count.</summary>
    private int Check(Room room, string path, WorldCheck world)
    {
        void Problem(string what) => _problems.Add($"{path}: {what}");
        bool Inside(Vector3 p) => p.X >= room.BoundsMin.X && p.X <= room.BoundsMax.X && p.Y >= room.BoundsMin.Y && p.Y <= room.BoundsMax.Y;

        foreach (string missing in _exports.Missing(room))
        {
            Problem(missing);
        }

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
            Extract(room, World.NameOf(path), exits, world);
        }

        return exits.Count;
    }

    /// <summary>The room's part of the map: its exits (one edge each, for leaving through it), relics, goals and shape.</summary>
    private void Extract(Room room, string name, List<RoomExit> exits, WorldCheck world)
    {
        static Box BoxOf(Block b) => Box.Around(b.GlobalPosition.Flat(), new NVector2(b.Size.X, b.Size.Y));
        var solids = room.Blocks().Select(BoxOf).ToList();
        var sound = room.Blocks().Where(b => !b.Cracked).Select(BoxOf).ToList();
        var exitShapes = exits.Where(e => e.TargetRoom == "" || ResourceLoader.Exists(e.TargetRoom))
            .Select(e => new ExitShape(e.Name, e.RoomPosition.ToCore(), e.Size.ToCore(), e.Requires, e.ArrivalSide, e.TargetRoom == "")).ToList();
        var wakes = room.All<Shrine>().Select(s => s.GlobalPosition.Flat()).ToList();
        if (World.RoomPath(name) == StartRoom)
        {
            wakes.Add(room.SpawnPoint.GlobalPosition.Flat());
        }

        var anchors = room.All<HarpoonAnchor>().Select(a => a.GlobalPosition.Flat()).ToList();
        var waters = room.All<WaterBody>().Where(w => w.Swim).Select(w => w.PlayRegion).ToList();
        var heat = room.All<HeatZone>().Select(z => z.Region).ToList();
        // Where walking takes a relic (its range from the feet) and reaches a goal (the feet inside its area), as trigger boxes.
        var relics = room.All<AbilityRelic>()
            .Select(r => (Box.Around(r.GlobalPosition.Flat() + new NVector2(0f, 0.1f), new NVector2(1.6f, 0.4f)), r.Ability)).ToList();
        var goals = room.All<MapGoal>().Where(g => g.Size != Vector2.Zero).Select(g => Box.Around(g.GlobalPosition.Flat(), g.Size.ToCore())).ToList();
        var metal = room.Blocks().Where(b => b.Surface == SurfaceKind.Metal && !b.Cracked).Select(BoxOf).ToList();
        world.AddRoom(name, room.BoundsMin.ToCore(), room.BoundsMax.ToCore(), new RoomShape(solids, exitShapes, wakes, anchors,
            room.All<Lift>().Select(l => l.Path).ToList(), sound, waters, heat, relics, goals, metal, room.SpawnPoint.GlobalPosition.Flat()));

        foreach (RoomExit exit in exits.Where(e => e.TargetRoom != "" && ResourceLoader.Exists(e.TargetRoom)))
        {
            if (Requirement.TryParse(exit.Requires, out Requirement requires, out _))
            {
                world.AddExit(new MapEdge(name, exit.Name, World.NameOf(exit.TargetRoom), exit.TargetExit, requires), exit.RoomPosition.ToCore(),
                    ArrivalPosition(exit.TargetRoom, exit.TargetExit));
            }
        }

        foreach (RoomExit exit in exits.Where(e => e.Sealed && Requirement.TryParse(e.Requires, out _, out _)))
        {
            world.AddSealed(name, exit.Requirement);
        }

        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            world.Graph.AddItem(new MapItem(name, relic.Name, relic.Ability));
        }

        if (room.All<MapGoal>().Any())
        {
            world.Graph.AddGoal(name);
        }
    }

    /// <summary>Where an exit sits in its room (the scene instanced but not run).</summary>
    private static NVector2? ArrivalPosition(string scene, string exitName)
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

            return p.Flat();
        }
        finally
        {
            target.Free();
        }
    }

    private MapDraft? LoadDraft()
    {
        string path = World.Definition.Network;
        try
        {
            return MapDraft.FromMarkdown(FileAccess.GetFileAsString(path));
        }
        catch (System.FormatException e)
        {
            _problems.Add($"map draft ({path}): {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// The world's network: its targets (<see cref="MapDraft.Check"/>), its layers (each one the world's), and its agreement with the
    /// built rooms; returns the summary line(s).
    /// </summary>
    private string CheckDraft(WorldCheck world)
    {
        if (_draft is not { } draft)
        {
            return "";
        }

        (MapReport report, List<string> problems) = draft.Check();
        _problems.AddRange(problems.Select(p => $"map draft: {p}"));
        _problems.AddRange(draft.Differences(world.Graph, world.Sealed).Select(p => $"map draft differs from the rooms: {p}"));
        _problems.AddRange(draft.LayerOrder.Where(l => World.Definition.Layer(l) is null)
            .Select(l => $"map draft: layer {l} is not one of the world's layers ({string.Join(", ", World.Definition.Layers.Select(w => w.Name))})"));

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
