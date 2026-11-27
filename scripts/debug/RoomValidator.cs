using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Checks every room scene of the world (<see cref="WorldDefinition.RoomsFolder"/>) and quits (`-- --validate-rooms`, run by
/// tools/check.sh). Prints `ROOMS OK` or one error per problem:
/// - the spawn point and every exit lie inside the camera bounds;
/// - gameplay blocks are axis-aligned with their edges on the block grid, kit pieces have their origin on the piece grid;
/// - exits have unique names, a valid requirement, an existing target room and exit facing the other way, and a target that leads
///   back unless they are one-way; each exit's trigger covers its whole opening and an arrival through it fits (<see cref="Threshold.Problems"/>);
/// - no mesh's top lies on a water surface over it (<see cref="WaterClearance"/>);
/// - each sealed arena holds its foes, lies inside the camera bounds and, its gates shut, leaves no way out (<see cref="CheckArenas"/>);
/// - the room has at least one photo spot;
/// - no required export is left empty, in the room's nodes, the bricks it places and the resources they reach, nor in the main
///   scene and its world (<see cref="RequiredExports"/>).
/// Each room is instantiated once; cross-room exit, door and arrival checks run over the cached exit snapshots afterward.
/// Delegates ground rules to <see cref="GroundCheck"/> and network/room-table checks to <see cref="DraftCheck"/>.
/// </summary>
public partial class RoomValidator : Node
{
    /// <summary>
    /// How close under or over a water surface the top of a mesh beneath it may lie, meters: closer, the top and the surface
    /// fight for the same depth, and the camera's sub-pixel jitter (TAA) shows one, then the other (docs/art-direction.md#stable-image).
    /// </summary>
    private const float WaterClearance = 0.05f;

    /// <summary>One exit's metadata cached from its room's single instantiation, for cross-room checks without reloading scenes.</summary>
    private sealed record ExitRecord(string Name, ExitSide Side, string TargetRoom, string TargetExit, string Requires,
        bool OneWay, bool ArrivalOnly, string? DoorItem, string? DoorName, NVector2 RoomPosition, bool InsideBounds);

    /// <summary>One room's cached exit table for cross-room validation and map graph edge wiring.</summary>
    private sealed record RoomRecord(string Path, string Name, bool InGame, List<ExitRecord> Exits);

    private readonly List<string> _problems = new();
    private readonly GroundCheck _ground = new();
    private readonly DraftCheck _draft = new();
    private readonly RequiredExports _exports = new();
    private readonly LineCheck _lines = new();
    private readonly SeamCheck _seams = new();
    private readonly HashSet<BiomeProfile> _biomes = new();
    private readonly Dictionary<string, RoomRecord> _roomsByPath = new(StringComparer.Ordinal);
    private readonly List<RoomRecord> _rooms = new();
    private bool _done;

    /// <summary>The world whose rooms are checked.</summary>
    public required World World { get; init; }

    /// <summary>The running game's banner, whose box every banner said must fit.</summary>
    public required Banner Banner { get; init; }

    /// <summary>The running game's hint line, whose one row every hint said must fit.</summary>
    public required HintLine Hint { get; init; }

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
        _draft.Load(World.Definition, _problems);
        var world = new WorldCheck(Tuning.Planner(ClimbRise));
        CheckMainScene();
        int rooms = 0, exits = 0;
        foreach (string path in World.RoomScenes())
        {
            if (World.Scene(path).Instantiate() is not Room room)
            {
                _problems.Add($"{path}: the root node is not a Room");
                continue;
            }

            AddChild(room);
            rooms++;
            exits += Check(room, path, world);
            _lines.Add(room, path);
            _biomes.Add(room.Biome);
            RemoveChild(room);
            room.Free();

            // Let the freed room's C# wrappers finalize now, while nothing else touches the engine: left to the finalizer thread
            // during the next room's setup, they intermittently crashed Godot (gchandle.is_released; the scenes too: World.Scene).
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        if (rooms == 0)
        {
            _problems.Add($"no room scene in {World.Definition.RoomsFolder}");
        }

        CheckCrossRoom(world);
        CheckBiomes();
        _lines.Add(World.Definition);
        _problems.AddRange(_lines.Problems(World.Definition.Lines));
        _problems.AddRange(LineCheck.MissingLetters(World.Definition.Lines, TextStyle.Shared.Font));
        _problems.AddRange(_lines.Unfit(World.Definition.Lines, Banner));
        _problems.AddRange(_lines.Overlong(World.Definition.Lines, Hint));
        CheckLockedLines(world.Graph);
        string map = world.Check(World.NameOf(StartRoom), Report);
        _problems.AddRange(world.Problems);
        if (ReachReport != "")
        {
            foreach (string line in world.ReachReport(ReachReport, ReachWith, SlackReport))
            {
                GD.Print(line);
            }
        }

        _problems.AddRange(_seams.Check(Tuning.Move.Height));
        map += "\n" + _draft.Check(World.Definition, world, Report, _problems);
        foreach (string problem in _problems)
        {
            GD.PushError($"ROOM CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"ROOMS OK: {rooms} rooms, {exits} exits");
            GD.Print(map);
        }

        GD.Print(_ground.Total());
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
        Node main = World.Scene(path).Instantiate();
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
            else if (Enum.TryParse(id, out Ability _))
            {
                _problems.Add($"world item {item.ResourcePath}: its id {id} is an ability's name");
            }
            else if (World.Definition.Items.Count(i => i.Id == id) > 1)
            {
                _problems.Add($"world item {item.ResourcePath}: another of the world's items has the id {id}");
            }
        }
    }

    /// <summary>Every profile the rooms wear and the world's layers name, held to <see cref="BiomeRules"/>.</summary>
    private void CheckBiomes()
    {
        foreach (WorldLayer layer in World.Definition.Layers)
        {
            _biomes.Add(layer.Biome);
        }

        foreach (BiomeProfile biome in _biomes)
        {
            _problems.AddRange(BiomeRules.Problems(biome.Palette()).Select(p => $"biome {biome.ResourcePath}: {p}"));
        }
    }

    /// <summary>Each item's locked line names the item and the room it lies in (docs/gameplay.md#gate-says), wherever the rooms lay it.</summary>
    private void CheckLockedLines(MapGraph graph)
    {
        foreach (ItemDefinition item in World.Definition.Items)
        {
            string? line = World.Definition.Lines.Find(item.LockedLine)?.Text, name = World.Definition.Lines.Find(item.NameLine)?.Text;
            if (line is null || name is null)
            {
                continue; // a missing line is the line check's to report
            }

            foreach (MapItem lying in graph.Items.Where(i => i.Item == item.Id))
            {
                foreach (string missing in LockedLine.Unnamed(line, name, lying.Room))
                {
                    _problems.Add($"world item {item.Id}: its locked line {item.LockedLine} (\"{line}\") does not name \"{missing}\" (the item lies in {lying.Room})");
                }
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

        List<Node> nodes = room.Descendants<Node>().Where(n => n.Owner == room).ToList();
        foreach (Block block in nodes.OfType<Block>())
        {
            Vector3 p = block.GlobalPosition;
            if (!block.GlobalBasis.IsEqualApprox(Basis.Identity))
            {
                Problem($"block {block.NameText()} is rotated; gameplay blocks stay axis-aligned");
            }

            if (!GridSnap.EdgesOnGrid(p.X, block.Size.X, GridSnap.BlockStep) || !GridSnap.EdgesOnGrid(p.Y, block.Size.Y, GridSnap.BlockStep))
            {
                Problem($"block {block.NameText()} at {p} size {block.Size} has edges off the {GridSnap.BlockStep} m grid");
            }
        }

        foreach (MeshInstance3D piece in nodes.OfType<MeshInstance3D>().Where(m => m.Mesh is KitMesh))
        {
            Vector3 p = piece.GlobalPosition;
            if (!GridSnap.OnGrid(p.X, GridSnap.PieceStep) || !GridSnap.OnGrid(p.Y, GridSnap.PieceStep) || !GridSnap.OnGrid(p.Z, GridSnap.PieceStep))
            {
                Problem($"kit piece {piece.NameText()} at {p} is off the {GridSnap.PieceStep} m grid");
            }
        }

        List<RoomExit> exits = nodes.OfType<RoomExit>().ToList();
        foreach (IGrouping<string, RoomExit> same in exits.GroupBy(e => e.NameText()).Where(g => g.Count() > 1))
        {
            Problem($"{same.Count()} exits are named {same.Key}");
        }

        var solids = room.Blocks().Select(RoomShapes.BoxOf).ToList();
        var body = new NVector2(Tuning.Move.Width, Tuning.Move.Height);
        var bounds = new Box(room.BoundsMin.X, room.BoundsMin.Y, room.BoundsMax.X, room.BoundsMax.Y);
        var records = new List<ExitRecord>(exits.Count);
        foreach (RoomExit exit in exits)
        {
            records.Add(new ExitRecord(exit.NameText(), exit.Side, exit.TargetRoom, exit.TargetExit, exit.Requires,
                exit.OneWay, exit.ArrivalOnly, exit.Door?.Item.Id, exit.Door?.NameText(), exit.RoomPosition.ToCore(), Inside(exit.GlobalPosition)));
            foreach (string problem in exit.Threshold.Problems(solids, bounds, body, left: !exit.ArrivalOnly))
            {
                Problem($"exit {exit.NameText()}: {problem}");
            }
        }

        string name = World.NameOf(path);
        var roomRecord = new RoomRecord(path, name, room.InGame, records);
        _rooms.Add(roomRecord);
        _roomsByPath[path] = roomRecord;

        CheckRoomDoorsAndItems(room, exits, Problem);
        foreach (RoomExit exit in exits.Where(e => e.Hidden))
        {
            Box area = exit.Threshold.Area;
            var trigger = new Rect2(area.MinX, area.MinY, area.MaxX - area.MinX, area.MaxY - area.MinY);
            if (!room.All<FalseWall>().Any(w => w.Region.Intersects(trigger)))
            {
                Problem($"exit {exit.NameText()} is hidden, but no FalseWall covers it");
            }
        }

        CheckWaterClearance(room, Problem);
        foreach (SlagBody slag in room.All<SlagBody>())
        {
            foreach (string problem in SlagBasin.Problems(slag.PlayRegion, solids).Concat(SlagBasin.SunkenGround(slag.PlayRegion, solids, 0.5f * body.X)))
            {
                Problem($"slag {slag.NameText()}: {problem}");
            }
        }

        var openings = exits.Select(e => e.Threshold.Area).ToList();
        foreach (HeatColumn heat in room.All<HeatColumn>())
        {
            foreach (string problem in HeatGate.Problems(heat.Region, solids, openings, bounds, body))
            {
                Problem($"heat {heat.NameText()}: {problem}");
            }
        }

        _ground.Check(room, name, Tuning, _problems);
        CheckArenas(room, bounds, Problem);

        if (!room.PhotoSpots().Any())
        {
            Problem("no PhotoSpot (tools/capture.sh needs at least one)");
        }

        if (room.InGame)
        {
            Extract(room, name, exits, world);
        }

        return exits.Count;
    }

    /// <summary>
    /// Cross-room exit, locked-door and map-edge checks over the cached <see cref="RoomRecord"/>s, avoiding re-instantiating
    /// target scenes for every exit in the world.
    /// </summary>
    private void CheckCrossRoom(WorldCheck world)
    {
        foreach (RoomRecord room in _rooms)
        {
            void Problem(string what) => _problems.Add($"{room.Path}: {what}");
            foreach (ExitRecord exit in room.Exits)
            {
                ExitRecord? arrival = FindArrival(exit.TargetRoom, exit.TargetExit);
                CheckExitLink(exit, room.Path, arrival, Problem);
                CheckDoorLink(exit, arrival, Problem);
                if (room.InGame && exit.TargetRoom != "" && (_roomsByPath.ContainsKey(exit.TargetRoom) || ResourceLoader.Exists(exit.TargetRoom))
                    && Requirement.TryParse(exit.Requires, out Requirement requires, out _))
                {
                    world.AddExit(new MapEdge(room.Name, exit.Name, World.NameOf(exit.TargetRoom), exit.TargetExit, requires),
                        exit.RoomPosition, arrival?.RoomPosition);
                }
            }
        }
    }

    private ExitRecord? FindArrival(string targetRoom, string targetExit)
    {
        if (targetRoom == "" || !_roomsByPath.TryGetValue(targetRoom, out RoomRecord? target))
        {
            return null;
        }

        return target.Exits.FirstOrDefault(e => e.Name == targetExit);
    }

    private void CheckExitLink(ExitRecord exit, string path, ExitRecord? arrival, Action<string> problem)
    {
        if (!exit.InsideBounds)
        {
            problem($"exit {exit.Name} is outside the camera bounds");
        }

        if (!Requirement.TryParse(exit.Requires, out _, out string error))
        {
            problem($"exit {exit.Name}: {error}");
        }

        if (exit.ArrivalOnly)
        {
            if (exit.TargetRoom != "" || exit.Requires != "")
            {
                problem($"exit {exit.Name} is arrival-only, so it has no target and no requirement");
            }

            return;
        }

        if (exit.TargetRoom == "")
        {
            return; // leads to a room not built yet
        }

        if (!_roomsByPath.ContainsKey(exit.TargetRoom) && !ResourceLoader.Exists(exit.TargetRoom))
        {
            problem($"exit {exit.Name} leads to {exit.TargetRoom}, which does not exist");
            return;
        }

        if (arrival is null)
        {
            problem($"exit {exit.Name} leads to {exit.TargetRoom}, which has no RoomExit named '{exit.TargetExit}'");
        }
        else if (!Threshold.Opposite(exit.Side, arrival.Side))
        {
            problem($"exit {exit.Name} leads {exit.Side}, but {exit.TargetRoom}:{exit.TargetExit} leads {arrival.Side}: the two ends of a way face opposite ways");
        }
        else if (!exit.OneWay && (arrival.TargetRoom != path || arrival.TargetExit != exit.Name))
        {
            problem($"exit {exit.Name} is two-way, but {exit.TargetRoom}:{exit.TargetExit} does not lead back to it (or mark it OneWay)");
        }
    }

    private static void CheckDoorLink(ExitRecord exit, ExitRecord? arrival, Action<string> problem)
    {
        if (exit.ArrivalOnly || !Requirement.TryParse(exit.Requires, out Requirement requires, out _))
        {
            return;
        }

        if (exit.DoorItem is { } doorItem && requires.Options.Any(o => !o.Items.Contains(doorItem)))
        {
            problem($"exit {exit.Name} is barred by {exit.DoorName}, so every alternative of its requirement needs {doorItem} (it is \"{exit.Requires}\")");
        }

        if (exit.DoorItem is { } barredItem && !exit.OneWay && arrival is not null
            && (!Requirement.TryParse(arrival.Requires, out Requirement back, out _) || back.Options.Any(o => !o.Items.Contains(barredItem))))
        {
            problem($"exit {exit.Name} is barred by {exit.DoorName}, so the way back ({exit.TargetRoom}:{exit.TargetExit}) needs {barredItem} " +
                    "too, in every alternative (or mark this exit OneWay)");
        }

        foreach (string item in requires.Items.Ids)
        {
            bool here = exit.DoorItem == item, there = arrival?.DoorItem == item;
            if (!here && !there)
            {
                problem($"exit {exit.Name} needs {item}, but no locked door for it bars this way (on either side)");
            }
        }
    }

    /// <summary>Room-local checks on locked doors, item pickups and levers.</summary>
    private void CheckRoomDoorsAndItems(Room room, List<RoomExit> exits, Action<string> problem)
    {
        foreach (LockedDoor door in room.All<LockedDoor>())
        {
            int barring = exits.Count(e => e.Door == door);
            if (barring != 1)
            {
                problem($"locked door {door.NameText()} bars {barring} exits (point one exit's Door at it)");
            }
        }

        foreach (ItemPickup pickup in room.All<ItemPickup>())
        {
            if (World.Definition.Item(pickup.Item.Id) != pickup.Item)
            {
                problem($"item {pickup.NameText()} lays {pickup.Item.Id} ({pickup.Item.ResourcePath}), which is not one of the world's items");
            }
            else if (pickup.Item.Kind == ItemKind.Mechanism)
            {
                problem($"item {pickup.NameText()} lays {pickup.Item.Id}, a mechanism: a Lever sets one, nothing picks it up");
            }
        }

        foreach (Lever lever in room.All<Lever>())
        {
            if (World.Definition.Item(lever.Item.Id) != lever.Item || lever.Item.Kind != ItemKind.Mechanism)
            {
                problem($"lever {lever.NameText()} sets {lever.Item.Id} ({lever.Item.ResourcePath}), which is not one of the world's mechanisms");
            }
        }
    }

    /// <summary>
    /// Each sealed arena held to its room (<see cref="SealedArena.Problems"/>): its foes inside its area, the area inside the camera
    /// bounds, and, its gates shut, no way out of the room the pilgrim reaches from inside it with every ability the map knows.
    /// </summary>
    private void CheckArenas(Room room, Box bounds, Action<string> problem)
    {
        IReadOnlyList<Arena> arenas = room.All<Arena>();
        if (arenas.Count == 0)
        {
            return;
        }

        RoomShape shape = RoomShapes.Of(room, start: false);
        var waysOut = room.Exits().Where(e => !e.ArrivalOnly).Select(e => (e.NameText(), e.Threshold.Trigger)).ToList();
        foreach (Arena arena in arenas)
        {
            var sealedArena = new SealedArena(arena.NameText(), arena.Area, arena.Gates.Select(g => g.ShutBox).ToList(),
                arena.Foes.Select(f => (f.NameText(), f.PlanePosition.ToCore())).ToList());
            foreach (string what in sealedArena.Problems(shape, bounds, waysOut, Tuning.Planner(ClimbRise), AbilitySet.Every))
            {
                problem(what);
            }
        }
    }

    /// <summary>Every mesh of the room (dressing included) whose top lies within <see cref="WaterClearance"/> of a water surface spread over it.</summary>
    private static void CheckWaterClearance(Room room, Action<string> problem)
    {
        IReadOnlyList<WaterBody> waters = room.All<WaterBody>();
        foreach (MeshInstance3D mesh in room.Descendants<Node>().OfType<MeshInstance3D>().Where(m => m.Mesh is not null))
        {
            if (waters.Any(w => w.IsAncestorOf(mesh)))
            {
                continue;
            }

            Aabb box = mesh.GlobalTransform * mesh.GetAabb();
            var footprint = new Rect2(box.Position.X, box.Position.Z, box.Size.X, box.Size.Z);
            foreach (WaterBody water in waters.Where(w => w.Area.Intersects(footprint) && Mathf.Abs(box.End.Y - w.SurfaceY) < WaterClearance))
            {
                problem($"{room.PathTextTo(mesh)}'s top at y {box.End.Y:0.###} lies on {water.NameText()}'s surface (y {water.SurfaceY:0.###}): keep it {WaterClearance} m clear");
            }
        }
    }

    /// <summary>The room's part of the map: its shape (relics, items and goals included), sealed exits, seams and draft elements.</summary>
    private void Extract(Room room, string name, List<RoomExit> exits, WorldCheck world)
    {
        RoomShape shape = RoomShapes.Of(room, World.RoomPath(name) == StartRoom);
        world.AddRoom(name, room.BoundsMin.ToCore(), room.BoundsMax.ToCore(), shape);
        _problems.AddRange(GripGaps.Problems(shape.Metal, Tuning.Move.Width).Select(p => $"{name}: {p}"));

        foreach (RoomExit exit in exits.Where(e => e.Sealed && Requirement.TryParse(e.Requires, out _, out _)))
        {
            world.AddSealed(name, exit.Requirement);
        }

        AddSeams(room, name, exits, shape.Solids);
        _draft.Record(room, name, exits);

        foreach (MapGoal goal in room.All<MapGoal>().Where(g => g.Size == Godot.Vector2.Zero))
        {
            _problems.Add($"{name}: the goal {goal.NameText()} has no area (Size is zero): the pilgrim could never reach it");
        }
    }

    private void AddSeams(Room room, string name, List<RoomExit> exits, List<Box> solids)
    {
        var waters = room.All<WaterBody>().Where(w => w.Area.Position.Y <= 0f && 0f <= w.Area.End.Y)
            .Select(w => (w.NameText(), w.PlayRegion)).ToList();
        _seams.AddRoom(name, new SeamRoom(new Box(room.BoundsMin.X, room.BoundsMin.Y, room.BoundsMax.X, room.BoundsMax.Y), solids, waters,
            room.All<Lift>().Select(l => l.Path).ToList(), exits.ToDictionary(e => e.NameText(), e => e.Threshold)));
        foreach (RoomExit exit in exits.Where(e => e.TargetRoom != "" && e.TargetExit != ""))
        {
            _seams.AddWay(name, exit.NameText(), World.NameOf(exit.TargetRoom), exit.TargetExit);
        }
    }
}
