using System.Collections.Generic;
using System.Globalization;
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
/// - exits have unique names, a valid requirement, an existing target room and exit facing the other way, and a target that leads
///   back unless they are one-way; each exit's trigger covers its whole opening and an arrival through it fits (<see cref="Threshold.Problems"/>);
/// - no mesh's top lies on a water surface over it (<see cref="WaterClearance"/>);
/// - the room has at least one photo spot;
/// - no required export is left empty, in the room's nodes, the bricks it places and the resources they reach, nor in the main
///   scene and its world (<see cref="RequiredExports"/>).
/// Then the world's line book: every line the code, the rooms and the world's items name is in it or its base (<see cref="LineCheck"/>).
/// Then the map, extracted from the rooms in the game (<see cref="Room.InGame"/>) and proven by the core's <see cref="WorldCheck"/>:
/// layout, reachability, softlocks, ability orders (`--map-report`: the whole graph), the geometry, the map from inside the rooms,
/// room for error; the seams, every way's two ends held to each other (<see cref="SeamCheck"/>, less the world's
/// <see cref="WorldDefinition.SeamExceptions"/>); and the world's network (<see cref="MapDraft"/>): its targets, its layers, its agreement with the built rooms, and its room
/// table held to it (<see cref="RoomTable.Check"/>; `--map-report`: each room's kinds).
/// Last, report-only: the ground rules' findings per room and their totals (`GROUND`, <see cref="ReportGround"/>). Run alone:
/// `godot --headless --path . -- --room MechanicsLab --validate-rooms | grep GROUND` (`--world sequel --room Strongroom` for the sequel).
/// </summary>
public partial class RoomValidator : Node
{
    private readonly List<string> _problems = new();

    /// <summary>What the ground rules (<see cref="GroundRules"/>) find, one line each; report-only until the Slag is rebuilt.</summary>
    private readonly List<string> _ground = new();

    /// <summary>Per room, how many findings of each kind the ground rules made (headroom, step, flush, coplanar, open mesh).</summary>
    private readonly List<(string Room, int[] Counts)> _groundCounts = new();

    private readonly RequiredExports _exports = new();
    private readonly LineCheck _lines = new();
    private readonly SeamCheck _seams = new();
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
            _lines.Add(room, path);
            RemoveChild(room);
            room.Free();

            // Let the freed room's C# wrappers finalize now, while nothing else touches the engine: left to the
            // finalizer thread during the next room's setup, they intermittently crashed Godot (gchandle.is_released).
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        _lines.Add(World.Definition);
        _problems.AddRange(_lines.Problems(World.Definition.Lines));
        string map = world.Check(World.NameOf(StartRoom), _draft?.Targets ?? MapTargets.None, Report);
        _problems.AddRange(world.Problems);
        if (ReachReport != "")
        {
            foreach (string line in world.ReachReport(ReachReport, ReachWith, SlackReport))
            {
                GD.Print(line);
            }
        }

        _problems.AddRange(_seams.Check(LoadSeamExceptions(), Tuning.Move.Height));
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

        foreach (string line in _ground)
        {
            GD.Print(line);
        }

        GD.Print(GroundTotal());
        if (_problems.Count > 0 && Report)
        {
            GD.Print(map); // the placements help find what doesn't line up
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }

    /// <summary>The main scene as authored (not as it runs): its nodes' required exports, and its world's; the world's item ids.</summary>
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

        foreach (ItemDefinition item in World.Definition.Items)
        {
            string id = item.Id;
            if (id == "" || !char.IsLetter(id[0]) || !id.All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                _problems.Add($"world item {item.ResourcePath}: its id \"{id}\" is not a word starting with a letter");
            }
            else if (System.Enum.TryParse(id, out Ability _))
            {
                _problems.Add($"world item {item.ResourcePath}: its id {id} is an ability's name");
            }
            else if (World.Definition.Items.Count(i => i.Id == id) > 1)
            {
                _problems.Add($"world item {item.ResourcePath}: another of the world's items has the id {id}");
            }
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

        var solids = room.Blocks().Select(BoxOf).ToList();
        var body = new NVector2(Tuning.Move.Width, Tuning.Move.Height);
        foreach (RoomExit exit in exits)
        {
            CheckExit(exit, path, Inside(exit.GlobalPosition), Problem);
            foreach (string problem in exit.Threshold.Problems(solids, new Box(room.BoundsMin.X, room.BoundsMin.Y, room.BoundsMax.X, room.BoundsMax.Y), body, left: !exit.ArrivalOnly))
            {
                Problem($"exit {exit.Name}: {problem}");
            }
        }

        CheckDoors(room, exits, Problem);
        foreach (RoomExit exit in exits.Where(e => e.Hidden))
        {
            Box area = exit.Threshold.Area;
            var trigger = new Rect2(area.MinX, area.MinY, area.MaxX - area.MinX, area.MaxY - area.MinY);
            if (!room.All<FalseWall>().Any(w => w.Region.Intersects(trigger)))
            {
                Problem($"exit {exit.Name} is hidden, but no FalseWall covers it");
            }
        }

        CheckWaterClearance(room, Problem);
        foreach (SlagBody slag in room.All<SlagBody>())
        {
            foreach (string problem in SlagBasin.Problems(slag.PlayRegion, solids))
            {
                Problem($"slag {slag.Name}: {problem}");
            }
        }

        var openings = exits.Select(e => e.Threshold.Area).ToList();
        var bounds = new Box(room.BoundsMin.X, room.BoundsMin.Y, room.BoundsMax.X, room.BoundsMax.Y);
        foreach (HeatColumn heat in room.All<HeatColumn>())
        {
            foreach (string problem in HeatGate.Problems(heat.Region, solids, openings, bounds, body))
            {
                Problem($"heat {heat.Name}: {problem}");
            }
        }

        ReportGround(room, World.NameOf(path), exits);

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

    /// <summary>The ground rules' kinds of finding, in the order of <see cref="_groundCounts"/>.</summary>
    private static readonly string[] GroundKinds = { "headroom", "step", "flush", "coplanar", "open mesh" };

    /// <summary>
    /// The ground rules (<see cref="GroundRules"/>) on the room's blocks and meshes, one `GROUND &lt;Room&gt; &lt;kind&gt;:` line per finding:
    /// report-only (no problem) until `docs/roadmap.md#volcanic-rethink` rebuilds the Slag on its bricks and a rule holds everywhere.
    /// Exit openings are passages the room marks low (extended outward, where the doorway's lintel lies).
    /// </summary>
    private void ReportGround(Room room, string name, List<RoomExit> exits)
    {
        int[] counts = new int[GroundKinds.Length];
        void Found(int kind, string what)
        {
            counts[kind]++;
            _ground.Add($"GROUND {name} {GroundKinds[kind]}: {what}");
        }

        List<Block> blocks = room.Blocks().ToList();
        var solids = blocks.Select(BoxOf).ToList();
        float radius = 0.5f * Tuning.Move.Width;
        var low = new List<Box>();
        foreach (RoomExit exit in exits)
        {
            Box a = exit.Threshold.Area;
            low.Add(exit.Side switch
            {
                ExitSide.West => a with { MinX = a.MinX - 1000f, MaxX = a.MaxX + radius },
                ExitSide.East => a with { MinX = a.MinX - radius, MaxX = a.MaxX + 1000f },
                _ => a.Grown(radius),
            });
        }

        var bounds = new Box(room.BoundsMin.X, room.BoundsMin.Y, room.BoundsMax.X, room.BoundsMax.Y);
        foreach (LowCeiling c in GroundRules.Headroom(solids, Tuning.Move.JumpHeight + Tuning.Move.Height, Tuning.Move.Height, radius, low, bounds))
        {
            Found(0, $"{blocks[c.Ground].Name}'s top at y {c.Y:0.##}, x {c.MinX:0.##}..{c.MaxX:0.##}: {blocks[c.Ceiling].Name} {c.Clearance:0.##} m over it " +
                     $"(a jump's head reaches {Tuning.Move.JumpHeight + Tuning.Move.Height:0.##})");
        }

        foreach (Seam seam in GroundRules.Seams(solids, Tuning.Move.LedgeAssist))
        {
            string where = $"{blocks[seam.A].Name} and {blocks[seam.B].Name} at x {seam.X:0.##}, y {seam.Y:0.##}";
            switch (seam.Kind)
            {
                case SeamKind.Step: Found(1, $"{where}: a {seam.Size:0.##} m step on a walking surface (under the {Tuning.Move.LedgeAssist} m ledge assist)"); break;
                case SeamKind.Flush: Found(2, $"{where}: tops flush side by side on a walking surface (a ghost edge)"); break;
                default: Found(3, $"{where}: overlapping, a face {seam.Size:0.##} m long in one plane (z-fighting)"); break;
            }
        }

        foreach (MeshInstance3D mesh in Descendants(room).OfType<MeshInstance3D>().Where(m => m.Mesh is not null && m.IsVisibleInTree() && m is not LightShaft))
        {
            if (Ancestors(mesh).Any(n => n is CollisionObject3D or WaterBody or HeatColumn or SlagBody) || mesh.GetChildren().Any(n => n is CollisionObject3D))
            {
                continue;
            }

            Aabb box = mesh.GlobalTransform * mesh.GetAabb();
            if (box.Position.Z >= -radius || box.End.Z <= radius)
            {
                continue; // wholly in front of or behind the pilgrim's body
            }

            var plane = new Box(box.Position.X, box.Position.Y, box.End.X, box.End.Y);
            foreach ((float from, float to) in GroundRules.OpenSpans(plane, solids))
            {
                Found(4, $"{room.GetPathTo(mesh)} lies across the play plane (y {plane.MinY:0.##}..{plane.MaxY:0.##}) with no solid from x {from:0.##} to {to:0.##}: the pilgrim passes through it");
            }
        }

        if (counts.Any(c => c > 0))
        {
            _groundCounts.Add((name, counts));
        }
    }

    /// <summary>The ground rules' totals over the world, and the rooms with the most findings.</summary>
    private string GroundTotal()
    {
        string kinds = string.Join(", ", GroundKinds.Select((k, i) => $"{k} {_groundCounts.Sum(r => r.Counts[i])}"));
        string top = string.Join(", ", _groundCounts.OrderByDescending(r => r.Counts.Sum()).Take(5)
            .Select(r => $"{r.Room} {string.Join("/", r.Counts)}"));
        return $"GROUND REPORT (report-only, docs/roadmap.md#volcanic-rethink): {_groundCounts.Count} rooms with findings; {kinds}; most: {top}";
    }

    private static IEnumerable<Node> Ancestors(Node node)
    {
        for (Node? n = node.GetParent(); n is not null; n = n.GetParent())
        {
            yield return n;
        }
    }

    /// <summary>
    /// How close under or over a water surface the top of a mesh beneath it may lie, meters: closer, the top and the surface
    /// fight for the same depth, and the camera's sub-pixel jitter (TAA) shows one, then the other (docs/art-direction.md#stable-image).
    /// </summary>
    private const float WaterClearance = 0.05f;

    /// <summary>Every mesh of the room (dressing included) whose top lies within <see cref="WaterClearance"/> of a water surface spread over it.</summary>
    private static void CheckWaterClearance(Room room, System.Action<string> problem)
    {
        IReadOnlyList<WaterBody> waters = room.All<WaterBody>();
        foreach (MeshInstance3D mesh in Descendants(room).OfType<MeshInstance3D>().Where(m => m.Mesh is not null))
        {
            if (waters.Any(w => w.IsAncestorOf(mesh)))
            {
                continue;
            }

            Aabb box = mesh.GlobalTransform * mesh.GetAabb();
            var footprint = new Rect2(box.Position.X, box.Position.Z, box.Size.X, box.Size.Z);
            foreach (WaterBody water in waters.Where(w => w.Area.Intersects(footprint) && Mathf.Abs(box.End.Y - w.SurfaceY) < WaterClearance))
            {
                problem($"{room.GetPathTo(mesh)}'s top at y {box.End.Y:0.###} lies on {water.Name}'s surface (y {water.SurfaceY:0.###}): keep it {WaterClearance} m clear");
            }
        }
    }

    /// <summary>The room's part of the map: its exits (one edge each, for leaving through it), relics, items, goals and shape.</summary>
    private void Extract(Room room, string name, List<RoomExit> exits, WorldCheck world)
    {
        var solids = room.Blocks().Select(BoxOf).ToList();
        var sound = room.Blocks().Where(b => !b.Cracked).Select(BoxOf).ToList();
        var exitShapes = exits.Where(e => e.TargetRoom == "" || ResourceLoader.Exists(e.TargetRoom))
            .Select(e => new ExitShape(e.Name, e.Threshold, e.Requires, e.ArrivalSide, e.TargetRoom == "")).ToList();
        var wakes = room.All<Shrine>().Select(s => s.GlobalPosition.Flat()).ToList();
        if (World.RoomPath(name) == StartRoom)
        {
            wakes.Add(room.SpawnPoint.GlobalPosition.Flat());
        }

        var anchors = room.All<HarpoonAnchor>().Select(a => a.GlobalPosition.Flat()).ToList();
        var waters = room.All<WaterBody>().Where(w => w.Swim).Select(w => w.PlayRegion).ToList();
        var heat = room.All<HeatColumn>().Select(z => z.Region).ToList();
        // Where walking takes a relic (its range from the feet) and reaches a goal (the feet inside its area), as trigger boxes.
        var relics = room.All<AbilityRelic>()
            .Select(r => (Box.Around(r.GlobalPosition.Flat() + new NVector2(0f, 0.1f), new NVector2(1.6f, 0.4f)), r.Ability)).ToList();
        var items = room.All<ItemPickup>()
            .Select(i => (Box.Around(i.GlobalPosition.Flat() + new NVector2(0f, 0.1f), new NVector2(1.6f, 0.4f)), i.Item.Id))
            .Concat(room.All<Lever>().Select(l => (Box.Around(l.GlobalPosition.Flat() + new NVector2(0f, 0.1f), new NVector2(2f * l.Range, 0.4f)), l.Item.Id)))
            .ToList();
        var goals = room.All<MapGoal>().Where(g => g.Size != Vector2.Zero).Select(g => Box.Around(g.GlobalPosition.Flat(), g.Size.ToCore())).ToList();
        var metal = room.Blocks().Where(b => b.Surface == SurfaceKind.Metal && !b.Cracked).Select(BoxOf).ToList();
        world.AddRoom(name, room.BoundsMin.ToCore(), room.BoundsMax.ToCore(), new RoomShape(solids, exitShapes, wakes, anchors,
            room.All<Lift>().Select(l => l.Path).ToList(), sound, waters, heat, relics, goals, metal, room.SpawnPoint.GlobalPosition.Flat())
        {
            Items = items,
            Slag = room.All<SlagBody>().Select(s => s.PlayRegion).ToList(),
        });

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

        AddSeams(room, name, exits, solids);
        _powerUps.AddRange(room.All<PowerUp>().Select(p => (name, p.Stat)));

        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            world.Graph.AddRelic(new MapRelic(name, relic.Name, relic.Ability));
        }

        foreach (ItemPickup pickup in room.All<ItemPickup>())
        {
            world.Graph.AddItem(new MapItem(name, pickup.Name, pickup.Item.Id));
        }

        foreach (Lever lever in room.All<Lever>())
        {
            world.Graph.AddItem(new MapItem(name, lever.Name, lever.Item.Id));
        }

        if (room.All<MapGoal>().Any())
        {
            world.Graph.AddGoal(name);
        }
    }

    private static Box BoxOf(Block b) => Box.Around(b.GlobalPosition.Flat(), new NVector2(b.Size.X, b.Size.Y));

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

    /// <summary>
    /// The room as the seam check sees it (<see cref="SeamCheck"/>): its solids, the water crossing its play plane (scenery too:
    /// it shows where it ends), its lifts and exits, and the ways from its exits.
    /// </summary>
    private void AddSeams(Room room, string name, List<RoomExit> exits, List<Box> solids)
    {
        var waters = room.All<WaterBody>().Where(w => w.Area.Position.Y <= 0f && 0f <= w.Area.End.Y)
            .Select(w => (w.Name.ToString(), w.PlayRegion)).ToList();
        _seams.AddRoom(name, new SeamRoom(new Box(room.BoundsMin.X, room.BoundsMin.Y, room.BoundsMax.X, room.BoundsMax.Y), solids, waters,
            room.All<Lift>().Select(l => l.Path).ToList(), exits.ToDictionary(e => e.Name.ToString(), e => e.Threshold)));
        foreach (RoomExit exit in exits.Where(e => e.TargetRoom != "" && e.TargetExit != ""))
        {
            _seams.AddWay(name, exit.Name, World.NameOf(exit.TargetRoom), exit.TargetExit);
        }
    }

    /// <summary>The world's seam exceptions (<see cref="SeamExceptions"/>); a world without them has none, one that doesn't parse is a problem.</summary>
    private SeamExceptions LoadSeamExceptions()
    {
        switch (World.Definition.LoadSeamExceptions())
        {
            case WorldData<SeamExceptions>.Loaded(var exceptions):
                return exceptions;
            case WorldData<SeamExceptions>.Malformed(var message):
                _problems.Add($"seam exceptions ({World.Definition.SeamExceptions}): {message}");
                return SeamExceptions.None;
            default:
                return SeamExceptions.None;
        }
    }

    /// <summary>The world's network; one that doesn't parse is a problem, a world without one is checked as an empty network.</summary>
    private MapDraft? LoadDraft()
    {
        switch (World.Definition.LoadNetwork())
        {
            case WorldData<MapDraft>.Loaded(var draft):
                return draft;
            case WorldData<MapDraft>.Malformed(var message):
                _problems.Add($"map draft ({World.Definition.Network}): {message}");
                return null;
            default:
                return new MapDraft();
        }
    }

    /// <summary>Writes the network's drawing (<see cref="MapDrawing"/>) to lookdev/map-WORLD.svg, WORLD the network's folder; returns its path.</summary>
    private string WriteDrawing(MapDraft draft, RoomTable? table)
    {
        string world = World.Definition.Network.GetBaseDir().GetFile();
        string path = $"res://lookdev/map-{world}.svg";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://lookdev"));
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(MapDrawing.Svg(draft, table, $"{world}: the network"));
        return ProjectSettings.GlobalizePath(path);
    }

    /// <summary>The world's room table (<see cref="RoomTable"/>); a world without one is a problem.</summary>
    private RoomTable? LoadTable()
    {
        string path = World.Definition.RoomTable;
        switch (World.Definition.LoadRoomTable())
        {
            case WorldData<RoomTable>.Loaded(var table):
                return table;
            case WorldData<RoomTable>.Malformed(var message):
                _problems.Add($"room table ({path}): {message}");
                return null;
            default:
                _problems.Add($"room table: the world names none, or {path} is missing (docs/roadmap.md#stage-plan)");
                return null;
        }
    }

    /// <summary>The built rooms' power-ups, as the room and the stat, gathered by <see cref="Extract"/>.</summary>
    private readonly List<(string Room, PowerStat Stat)> _powerUps = new();

    /// <summary>Where the network's <c>powerup</c> lines and the built rooms' <see cref="PowerUp"/> nodes disagree, room by room.</summary>
    private IEnumerable<string> PowerUpDifferences(MapDraft draft)
    {
        static string Of(IEnumerable<(string Room, PowerStat Stat)> list, string room) =>
            string.Join(", ", list.Where(p => p.Room == room).Select(p => p.Stat.ToString().ToLowerInvariant()).Order(System.StringComparer.Ordinal));

        foreach (string room in draft.PowerUps.Concat(_powerUps).Select(p => p.Room).Distinct())
        {
            string said = Of(draft.PowerUps, room), built = Of(_powerUps, room);
            if (said != built)
            {
                yield return $"{room}: the network's power-ups are [{said}], the room holds [{built}]";
            }
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
        _problems.AddRange(PowerUpDifferences(draft).Select(p => $"map draft differs from the rooms: {p}"));
        _problems.AddRange(draft.LayerOrder.Where(l => World.Definition.Layer(l) is null)
            .Select(l => $"map draft: layer {l} is not one of the world's layers ({string.Join(", ", World.Definition.Layers.Select(w => w.Name))})"));

        RoomTable? table = LoadTable();
        if (table is not null)
        {
            _problems.AddRange(table.Check(draft).Select(p => $"room table ({World.Definition.RoomTable}): {p}"));
        }

        var text = new System.Text.StringBuilder($"DRAFT OK: {draft.Summary(report)}");
        if (table is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"; room table: {table.Rows.Count} rows, held to the network");
        }

        if (Report)
        {
            text.Append(CultureInfo.InvariantCulture, $"\ndrawing: {WriteDrawing(draft, table)}");
            if (table is not null)
            {
                text.Append("\nroom table (kinds):");
                foreach (RoomRow row in table.Rows)
                {
                    text.Append(CultureInfo.InvariantCulture, $"\n  {row.Room} ({row.Layer}): {string.Join(", ", row.Kinds).ToLowerInvariant()}");
                }
            }

            text.Append("\ndraft ways:");
            foreach (MapEdge e in draft.Graph.Edges)
            {
                string needs = e.Requires.ToString();
                text.Append(CultureInfo.InvariantCulture, $"\n  {e.From} → {e.To}{(needs == "" ? "" : $"  needs {needs}")}");
            }

            foreach (Ability[] order in report.Orders)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n  draft order: {string.Join(" → ", order)}");
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Locked doors against the exits they bar: each door is one exit's (<see cref="RoomExit.Door"/>), its item is in every alternative
    /// of that exit's requirement and of the way back's, and an exit whose requirement names an item has the door for it, or leads
    /// through one (the way back of a door in the next room). Every item lying here is one of the world's.
    /// </summary>
    private void CheckDoors(Room room, List<RoomExit> exits, System.Action<string> problem)
    {
        foreach (LockedDoor door in room.All<LockedDoor>())
        {
            int barring = exits.Count(e => e.Door == door);
            if (barring != 1)
            {
                problem($"locked door {door.Name} bars {barring} exits (point one exit's Door at it)");
            }
        }

        foreach (RoomExit exit in exits.Where(e => !e.ArrivalOnly && Requirement.TryParse(e.Requires, out _, out _)))
        {
            Requirement requires = exit.Requirement;
            if (exit.Door is { } door && requires.Options.Any(o => !o.Items.Contains(door.Item.Id)))
            {
                problem($"exit {exit.Name} is barred by {door.Name}, so every alternative of its requirement needs {door.Item.Id} (it is \"{exit.Requires}\")");
            }

            WayBack? arrival = ArrivalOf(exit);
            if (exit.Door is { } barred && !exit.OneWay && arrival is { } way
                && (!Requirement.TryParse(way.Requires, out Requirement back, out _) || back.Options.Any(o => !o.Items.Contains(barred.Item.Id))))
            {
                problem($"exit {exit.Name} is barred by {barred.Name}, so the way back ({exit.TargetRoom}:{exit.TargetExit}) needs {barred.Item.Id} " +
                        "too, in every alternative (or mark this exit OneWay)");
            }

            foreach (string item in requires.Items.Ids)
            {
                bool here = exit.Door?.Item.Id == item, there = arrival?.DoorItem == item;
                if (!here && !there)
                {
                    problem($"exit {exit.Name} needs {item}, but no locked door for it bars this way (on either side)");
                }
            }
        }

        foreach (ItemPickup pickup in room.All<ItemPickup>())
        {
            if (World.Definition.Item(pickup.Item.Id) != pickup.Item)
            {
                problem($"item {pickup.Name} lays {pickup.Item.Id} ({pickup.Item.ResourcePath}), which is not one of the world's items");
            }
            else if (pickup.Item.Kind == ItemKind.Mechanism)
            {
                problem($"item {pickup.Name} lays {pickup.Item.Id}, a mechanism: a Lever sets one, nothing picks it up");
            }
        }

        foreach (Lever lever in room.All<Lever>())
        {
            if (World.Definition.Item(lever.Item.Id) != lever.Item || lever.Item.Kind != ItemKind.Mechanism)
            {
                problem($"lever {lever.Name} sets {lever.Item.Id} ({lever.Item.ResourcePath}), which is not one of the world's mechanisms");
            }
        }
    }

    /// <summary>What the door checks read of the exit a way arrives through: its requirement, and the item of the door barring it.</summary>
    private readonly record struct WayBack(string Requires, string? DoorItem);

    /// <summary>The exit an exit leads to, in its target room (the scene instanced, then freed), or null.</summary>
    private static WayBack? ArrivalOf(RoomExit exit)
    {
        if (exit.TargetRoom == "" || !ResourceLoader.Exists(exit.TargetRoom))
        {
            return null;
        }

        Node target = GD.Load<PackedScene>(exit.TargetRoom).Instantiate();
        try
        {
            if (target.FindChild(exit.TargetExit, recursive: true, owned: true) is not RoomExit arrival)
            {
                return null;
            }

            return new WayBack(arrival.Requires, arrival.Door?.Item.Id);
        }
        finally
        {
            target.Free();
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
            else if (!Threshold.Opposite(exit.Side, arrival.Side))
            {
                problem($"exit {name} leads {exit.Side}, but {exit.TargetRoom}:{exit.TargetExit} leads {arrival.Side}: the two ends of a way face opposite ways");
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
