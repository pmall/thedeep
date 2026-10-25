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

    /// <summary>Order freedom target once all five abilities are on the map (docs/long-term.md): at least this many of the 120 orders.</summary>
    private const int OrderTarget = 24;

    private readonly List<string> _problems = new();
    private readonly MapGraph _graph = new();
    private readonly List<MapLink> _links = new();
    private readonly Dictionary<string, (System.Numerics.Vector2, System.Numerics.Vector2)> _bounds = new();
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

        string map = CheckMap();
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

        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            _graph.AddItem(new MapItem(name, relic.Name, relic.Ability));
        }

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

        if (report.AbilityCount == 5 && report.Orders.Count < OrderTarget)
        {
            _problems.Add($"map: only {report.Orders.Count} of {report.PossibleOrders} ability orders are achievable (target {OrderTarget})");
        }

        var text = new System.Text.StringBuilder();
        text.Append($"MAP OK: {_graph.Rooms.Count} rooms, {_graph.Edges.Count} exits, {_graph.Items.Count} relics, ")
            .Append($"{report.ReachableStates} states, no softlock, {report.Orders.Count} of {report.PossibleOrders} ability orders");
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
