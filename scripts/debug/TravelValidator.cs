using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Crosses every open exit of every room (`-- --validate-travel`, run by tools/check.sh) and quits. For each
/// exit: loads its room, puts the player in the exit's area moving out through it, and checks that the
/// player arrives in the target room inside the arrival exit's area, that the room is placed on the map,
/// and that the player is still there once it has landed and stood a second (no bounce back through the
/// arrival exit, and a drop through a hatch never falls on into a doorway below it). Prints
/// `TRAVEL OK` or one `TRAVEL CHECK` error per problem.
/// </summary>
public partial class TravelValidator : Node
{
    private const int TimeoutTicks = 120;
    private const int SettleTicks = 60;

    /// <summary>Longest wait for the player to land after arriving (a drop down a flue), ticks.</summary>
    private const int LandTicks = 360;

    public World World { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public RoomTravel Travel { get; set; } = null!;

    private readonly List<(string Room, string Exit)> _todo = new();
    private readonly List<string> _problems = new();
    private (string Room, string Exit, string TargetRoom, string TargetExit) _current;
    private int _tick;
    private bool _arrived;
    private int _crossed;
    private int _standing;

    public override void _Ready()
    {
        foreach (string path in DirAccess.GetFilesAt(World.RoomFolder).Where(f => f.EndsWith(".tscn")).Order())
        {
            var room = GD.Load<PackedScene>($"{World.RoomFolder}/{path}").Instantiate<Room>();
            foreach (RoomExit exit in Find(room).Where(e => e.TargetRoom != ""))
            {
                _todo.Add((World.NameOf(path), exit.Name));
            }

            room.Free();
        }

        Travel.Travelled += _ =>
        {
            _arrived = true;
            _tick = 0;
            _standing = 0;
        };
        Next();
    }

    private static IEnumerable<RoomExit> Find(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is RoomExit exit)
            {
                yield return exit;
            }

            foreach (RoomExit nested in Find(child))
            {
                yield return nested;
            }
        }
    }

    private void Next()
    {
        if (_todo.Count == 0)
        {
            Finish();
            return;
        }

        (string roomName, string exitName) = _todo[0];
        _todo.RemoveAt(0);
        Room room = World.Load(GD.Load<PackedScene>(World.RoomPath(roomName)));
        RoomExit exit = room.FindExit(exitName)!;
        _current = (roomName, exitName, World.NameOf(exit.TargetRoom), exit.TargetExit);

        // Moving out through the exit: up or down through a hatch, toward the nearer side through a doorway.
        Vector2 at = exit.RoomPosition;
        Vector2 center = (room.BoundsMin + room.BoundsMax) * 0.5f;
        Vector3 velocity = ExitMath.IsVertical(new System.Numerics.Vector2(exit.Size.X, exit.Size.Y))
            ? new Vector3(0f, at.Y > center.Y ? 10f : -10f, 0f)
            : new Vector3(at.X > center.X ? 7f : -7f, 0f, 0f);
        Player.Respawn(new Vector3(at.X, at.Y, 0f));
        Player.Velocity = velocity;
        _arrived = false;
        _tick = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_current.Room is null)
        {
            return;
        }

        _tick++;
        string what = $"{_current.Room}:{_current.Exit} → {_current.TargetRoom}:{_current.TargetExit}";
        if (!_arrived)
        {
            if (_tick > TimeoutTicks)
            {
                _problems.Add($"{what}: never travelled (is the exit's area reachable, and the target loadable?)");
                Next();
            }

            return;
        }

        _standing = Player.IsOnFloor() ? _standing + 1 : 0;
        if (_standing < SettleTicks && _tick < LandTicks)
        {
            return;
        }

        if (World.RoomName != _current.TargetRoom)
        {
            _problems.Add($"{what}: ended up in {World.RoomName} (bounced back through the arrival exit?)");
        }
        else if (!World.Map.Knows(_current.TargetRoom))
        {
            _problems.Add($"{what}: the room was not placed on the map");
        }

        _crossed++;
        _current = default;
        Next();
    }

    private void Finish()
    {
        foreach (string problem in _problems)
        {
            GD.PushError($"TRAVEL CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"TRAVEL OK: crossed {_crossed} exits");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
