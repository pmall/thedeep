using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Every room's own ground routes, as the reach planner flies them, played in the real game through the real input actions
/// (`-- --route-check`, run by tools/check.sh for both worlds; `--shard K/N` plays every Nth room; alone:
/// `godot --headless --path . --fixed-fps 60 --quit-after 20000 -- --route-check 2>&1 | grep ROUTE`), so the planner's model of the
/// moves and the real moves can't drift apart unseen. In each room in the game, <see cref="GroundRoutes.Pick"/> chooses up to
/// <see cref="PerRoom"/> of the planner's flights from the floors it reaches with no ability (each room starting from the next kind
/// of move, so every kind is played), keeping off every solid that is not a
/// block (a gate, a fence, a lift's cage: the planner doesn't see them) and the power-ups (coming near one takes it). The pilgrim,
/// with no ability, the machines blind to it and the arenas never sealing, plays each: set down behind the take-off running at full
/// speed (or standing on it), it holds the way, jumps at the take-off, lets go of jump when the planner did and dashes when the
/// planner did. A press is read on the tick after it is made, as a device's is, and a dash moves from the tick after it begins: each
/// is pressed that much earlier. Each route must land where the planner said, along its path (<see cref="GroundRoutes.Mismatch"/>);
/// a blow, a loss of control or leaving the room is a problem too. Prints `ROUTES OK` or one `ROUTE CHECK` error per problem.
/// </summary>
public partial class RouteCheck : Node
{
    /// <summary>The most routes played in one room.</summary>
    public const int PerRoom = 4;

    /// <summary>Ticks a route may run past the planned landing before it counts as never landed.</summary>
    private const int Overtime = 60;

    /// <summary>Ticks a standing take-off waits on its feet before the jump.</summary>
    private const int Settle = 3;

    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's machines, kept blind to the pilgrim.</summary>
    public required MachineDirector Machines { get; init; }
    /// <summary>The running game's power-ups, taken by coming near them: the routes keep off them.</summary>
    public required PowerUps PowerUps { get; init; }
    /// <summary>The moves as the planner simulates them (the live tuning).</summary>
    public required PlannerLimits Limits { get; init; }

    /// <summary>Which part of the rooms this run plays (`--shard K/N`: every Nth room from the Kth, 1-based); all by default.</summary>
    public (int Index, int Count) Shard { get; init; } = (1, 1);

    private readonly List<string> _rooms = new();
    private readonly List<GroundRoute> _routes = new();
    private readonly List<string> _problems = new();
    private readonly List<NVector2> _real = new();
    private int _roomIndex = -1, _routeIndex, _tick, _flight = -1, _played, _roomsPlayed, _jumps, _walkOffs, _dashes;
    private bool _airborne, _hurt;
    private string _roomName = "";

    /// <summary>Runs before the pilgrim each physics tick: it reads where the last move left the body, and what it holds or lets go of counts at once.</summary>
    public RouteCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private GroundRoute Current => _routes[_routeIndex];

    private NVector2 Feet => Player.GlobalPosition.Flat();

    public override void _Ready()
    {
        Player.Hurt += () => _hurt = true;
        foreach (Ability ability in AbilitySet.All)
        {
            Player.Abilities.Lock(ability); // the base moveset, which the planner's ground routes are flown with
        }

        Machines.PlayerPresent = false;
        _rooms.AddRange(World.RoomScenes().Where((_, i) => i % Shard.Count == Shard.Index - 1));
        NextRoom();
    }

    /// <summary>Loads the next room in the game and picks its routes.</summary>
    private void NextRoom()
    {
        _routes.Clear();
        _routeIndex = 0;
        while (_routes.Count == 0)
        {
            if (++_roomIndex >= _rooms.Count)
            {
                Finish();
                return;
            }

            Room room = World.Load(World.Scene(_rooms[_roomIndex]));
            if (!room.InGame)
            {
                continue;
            }

            _roomName = World.RoomName;
            foreach (Arena arena in room.All<Arena>())
            {
                arena.ProcessMode = ProcessModeEnum.Disabled; // never sealed: its gates stay up, as the planner sees the room
            }

            _routes.AddRange(GroundRoutes.Pick(RoomShapes.Of(room, start: false), Limits, PerRoom, Unseen(room).Concat(PowerUpAreas(room)), _roomsPlayed));
            _roomsPlayed++;
        }

        Start();
    }

    /// <summary>Where coming near a power-up takes it (and holds the pilgrim still), as boxes.</summary>
    private IEnumerable<Box> PowerUpAreas(Room room) =>
        room.All<PowerUp>().Select(p => Box.Around(p.GlobalPosition.Flat(), new NVector2(2f * PowerUps.TakeRange, 2f * PowerUps.TakeRange)));

    /// <summary>
    /// The room's solids that are not blocks (the planner doesn't see them): their boxes on the play plane. Each is a box shape; another
    /// shape is an error, for the check to learn it.
    /// </summary>
    private static List<Box> Unseen(Room room)
    {
        var bodies = new List<CollisionObject3D>();
        var boxes = new List<Box>();
        Collect(room, bodies);
        foreach (CollisionObject3D body in bodies)
        {
            foreach (CollisionShape3D shape in body.GetChildren().OfType<CollisionShape3D>().Where(s => !s.Disabled))
            {
                if (shape.Shape is not BoxShape3D cube)
                {
                    GD.PushError($"ROUTE CHECK: {room.Name}: {body.Name} is a solid of a shape the check can't keep off ({shape.Shape?.GetClass()})");
                    continue;
                }

                // Its eight corners on the play plane.
                Vector3 half = cube.Size * 0.5f;
                var corners = new List<Vector3>();
                foreach (float x in new[] { -half.X, half.X })
                {
                    foreach (float y in new[] { -half.Y, half.Y })
                    {
                        foreach (float z in new[] { -half.Z, half.Z })
                        {
                            corners.Add(shape.GlobalTransform * new Vector3(x, y, z));
                        }
                    }
                }

                boxes.Add(new Box(corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y)));
            }
        }

        return boxes;
    }

    private static void Collect(Node node, List<CollisionObject3D> bodies)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is CollisionObject3D body and not Block && (body.CollisionLayer & PhysicsLayers.Solid) != 0)
            {
                bodies.Add(body);
            }

            Collect(child, bodies);
        }
    }

    /// <summary>Sets the pilgrim down for the current route: behind a running take-off at full speed, or on a standing one.</summary>
    private void Start()
    {
        GroundRoute r = Current;
        int dir = r.Leap.Move.Dir;
        Release();
        Player.Vitality.Restore();
        Player.Respawn(r.Start.OnPlane(), dir == 0 ? 1 : dir);
        Player.Velocity = new Vector3(dir * Limits.Move.RunSpeed, 0f, 0f);
        Hold(dir);
        _tick = 0;
        _flight = -1;
        _airborne = _hurt = false;
        _real.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_roomIndex < 0 || _roomIndex >= _rooms.Count)
        {
            return;
        }

        _tick++;
        GroundRoute r = Current;
        if (World.RoomName != _roomName || _hurt || !Player.InControl)
        {
            Problem(World.RoomName != _roomName ? $"left the room for {World.RoomName}" : _hurt ? "hurt on the way" : "control lost on the way");
            if (World.RoomName != _roomName)
            {
                Finish(); // the game took the pilgrim elsewhere: what follows would not be this check's
                return;
            }

            Next();
            return;
        }

        GroundMove m = r.Leap.Move;
        if (_flight < 0)
        {
            // The run-up: the take-off comes on the tick whose move starts there (half a step either way). A press is read on the tick
            // after it is made, as a device's is, so the jump is pressed a tick before.
            float step = Limits.Move.RunSpeed / 60f, behind = m.Dir * (r.Leap.TakeOff.X - Feet.X);
            if (m.Dir == 0 ? _tick <= Settle : behind > 0.5f * step)
            {
                Set(InputActions.Jump, m.JumpTicks != 0 && (m.Dir == 0 ? _tick == Settle : behind <= 1.5f * step));
                if (_tick > GroundRoutes.RunUpTicks + Settle + 10)
                {
                    Problem($"never reached the take-off, at ({Feet.X:0.##}, {Feet.Y:0.##})");
                    Next();
                }

                return;
            }

            _flight = 0;
        }
        else
        {
            _flight++;
        }

        _real.Add(Feet);
        _airborne |= _flight > 0 && !Player.IsOnFloor();
        if (_flight > 0 && _airborne && Player.IsOnFloor() || _flight > r.Leap.Path.Count + Overtime)
        {
            _played++;
            _jumps += m.JumpTicks != 0 ? 1 : 0;
            _walkOffs += m.JumpTicks == 0 ? 1 : 0;
            _dashes += m.DashTick >= 0 ? 1 : 0;
            if (GroundRoutes.Mismatch(r, _real, landed: Player.IsOnFloor()) is { } problem)
            {
                Problem(problem);
            }

            Next();
            return;
        }

        // The planner's inputs at this tick of the flight: jump held as long as the planner held it; the dash pressed so that it begins
        // (moving from the tick after) on the tick before the planner's.
        Set(InputActions.Jump, m.JumpTicks != 0 && (m.JumpTicks < 0 || _flight < m.JumpTicks));
        Set(InputActions.Dash, m.DashTick >= 0 && _flight == m.DashTick - 2);
    }

    private void Next()
    {
        Release();
        if (++_routeIndex >= _routes.Count)
        {
            NextRoom();
        }
        else
        {
            Start();
        }
    }

    private static void Hold(int dir)
    {
        Set(InputActions.MoveLeft, dir < 0);
        Set(InputActions.MoveRight, dir > 0);
    }

    private static void Set(StringName action, bool held)
    {
        if (held && !Input.IsActionPressed(action))
        {
            Input.ActionPress(action);
        }
        else if (!held && Input.IsActionPressed(action))
        {
            Input.ActionRelease(action);
        }
    }

    private static void Release()
    {
        foreach (StringName action in new[] { InputActions.MoveLeft, InputActions.MoveRight, InputActions.Jump, InputActions.Dash })
        {
            Input.ActionRelease(action);
        }
    }

    private void Problem(string what) => _problems.Add($"{_roomName}: {Current.What}: {what}");

    private void Finish()
    {
        _roomIndex = _rooms.Count;
        Release();
        foreach (string problem in _problems)
        {
            GD.PushError($"ROUTE CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"ROUTES OK: {_played} ground routes in {_roomsPlayed} rooms ({_jumps} jumps, {_walkOffs} walk-offs, {_dashes} with an air dash), landed where the planner said" +
                     (Shard.Count > 1 ? $" (part {Shard.Index} of {Shard.Count})" : ""));
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
