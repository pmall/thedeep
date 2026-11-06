using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Crosses every open exit of every room in the running game (`-- --validate-travel`, run by tools/check.sh) and quits. For each exit:
/// - it is crossed at the bottom, the middle and the top of a doorway's opening (where the body fits), at the left, the middle and the
///   right of a hatch's, each time from a freshly loaded room, the body moving out and the inputs held as a player would (the move out
///   of a doorway; down, to sink in water with Ballast, except rising through a roof); each crossing must arrive in the target room,
///   clear of the arrival exit's trigger;
/// - after the last one the pilgrim must land and stand a second there, the room placed on the map (no bounce back through the arrival
///   exit, and a drop through a hatch never falls on into another exit below it);
/// - then the way back, if the exit is two-way: arrived through a doorway, the move held back out must travel back and stand in the room
///   left; arrived up through a hatch in the floor, crossing again and falling (or sinking) straight back down (no speed, no steering)
///   must travel back down and stand there.
/// That each trigger covers its whole opening, so no way of moving gets past it, is the room check's to prove (<see cref="Threshold.Problems"/>).
/// The pilgrim holds every ability. Prints `TRAVEL OK` or one `TRAVEL CHECK` error per problem.
/// </summary>
public partial class TravelValidator : Node
{
    /// <summary>Longest wait for a crossing to travel, ticks.</summary>
    private const int CrossTicks = 120;

    /// <summary>How long the pilgrim must stand still in a room it arrived in, ticks.</summary>
    private const int SettleTicks = 60;

    /// <summary>Longest wait for the pilgrim to land after arriving (a drop down a flue), ticks.</summary>
    private const int LandTicks = 360;

    /// <summary>Longest walk back out through the doorway just arrived through, ticks.</summary>
    private const int BackTicks = 240;

    public required World World { get; init; }
    public required Player Player { get; init; }
    public required RoomTravel Travel { get; init; }

    /// <summary>
    /// Which part of the exits this run crosses (`--shard K/N`: every Nth exit from the Kth, 1-based), so tools/check.sh crosses
    /// them in several Godots at once; the whole list by default.
    /// </summary>
    public (int Index, int Count) Shard { get; init; } = (1, 1);

    private enum Phase
    {
        /// <summary>Moving out through the exit at one place along its opening; then the next place, or standing.</summary>
        Crossing,

        /// <summary>Landing and standing in <see cref="_expected"/>.</summary>
        Standing,

        /// <summary>Holding the move back out through the doorway just arrived through.</summary>
        TurningBack,

        /// <summary>Crossing up through the hatch again, to fall straight back down on arriving.</summary>
        Climbing,

        /// <summary>Arrived up through the hatch with no speed and no steering: falling back down through it.</summary>
        FallingBack,
    }

    private readonly List<(string Room, string Exit)> _todo = new();
    private readonly List<string> _problems = new();
    private (string Room, string Exit, string TargetRoom, string TargetExit) _current;
    private readonly List<NVector2> _places = new();
    private NVector2 _place;
    private Phase _phase;
    private string _expected = "";
    private bool _back;
    private int _tick;
    private int _standing;
    private int _crossings;
    private int _exits;
    private bool _arrived;
    private readonly List<StringName> _held = new();

    public override void _Ready()
    {
        foreach (string path in World.RoomScenes())
        {
            var room = World.Scene(path).Instantiate<Room>();
            foreach (RoomExit exit in Find(room).Where(e => e.TargetRoom != ""))
            {
                _todo.Add((World.NameOf(path), exit.Name));
            }

            room.Free();
        }

        var mine = _todo.Where((_, i) => i % Shard.Count == Shard.Index - 1).ToList();
        _todo.Clear();
        _todo.AddRange(mine);

        // Every ability: this crosses exits, it doesn't test their requirements (and heat would burn a pilgrim without plating).
        foreach (Ability ability in AbilitySet.All)
        {
            Player.Abilities.Unlock(ability);
        }

        Travel.Travelled += _ => _arrived = true;
        NextExit();
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

    private string What => $"{_current.Room}:{_current.Exit} → {_current.TargetRoom}:{_current.TargetExit}";

    private void NextExit()
    {
        Hold();
        _current = default;
        if (_todo.Count == 0)
        {
            Finish();
            return;
        }

        (string roomName, string exitName) = _todo[0];
        _todo.RemoveAt(0);
        Room room = World.Load(World.Scene(World.RoomPath(roomName)));
        if (room.FindExit(exitName) is not { } exit)
        {
            _problems.Add($"{roomName}:{exitName}: the exit is gone from its room");
            NextExit();
            return;
        }

        _current = (roomName, exitName, World.NameOf(exit.TargetRoom), exit.TargetExit);
        _back = !exit.OneWay;
        _exits++;

        // Along the opening: a doorway's bottom, middle and top where the body fits, a hatch's left, middle and right.
        Threshold t = exit.Threshold;
        NVector2 body = Player.BodySize;
        _places.Clear();
        float slack = t.IsHatch ? (t.Size.X - body.X) * 0.5f - Threshold.Clearance : t.Size.Y - body.Y - Threshold.Clearance;
        List<Box> solids = room.Solids();
        NVector2 start = t.Arrival(body, solids);
        foreach (float along in slack > 0.05f ? (t.IsHatch ? new[] { 0f, -slack, slack } : new[] { 0f, slack * 0.5f, slack }) : new[] { 0f })
        {
            // Where the body fits: a ledge may hang over part of a hatch's hole.
            NVector2 offset = t.IsHatch ? new NVector2(t.Marker.X + along - start.X, 0f) : new NVector2(0f, along);
            NVector2 feet = start + offset;
            if (!solids.Any(s => s.Overlaps(new Box(feet.X - body.X * 0.5f, feet.Y, feet.X + body.X * 0.5f, feet.Y + body.Y))))
            {
                _places.Add(offset);
            }
        }

        if (_places.Count == 0)
        {
            _places.Add(NVector2.Zero);
        }

        Launch(exit, _places[0], Phase.Crossing);
    }

    /// <summary>Loads the exit's room afresh and moves the body out through the exit, <paramref name="offset"/> along its opening.</summary>
    private void Cross(NVector2 offset, Phase phase)
    {
        Room room = World.Load(World.Scene(World.RoomPath(_current.Room)));
        if (room.FindExit(_current.Exit) is not { } exit)
        {
            _problems.Add($"{What}: the exit is gone from its room");
            NextExit();
            return;
        }

        Launch(exit, offset, phase);
    }

    /// <summary>
    /// Puts the body just inside <paramref name="exit"/>, <paramref name="offset"/> along its opening (up a doorway, in the air there;
    /// across a hatch), moving out through it with what a player holds.
    /// </summary>
    private void Launch(RoomExit exit, NVector2 offset, Phase phase)
    {
        Threshold t = exit.Threshold;
        _place = offset;
        Player.Respawn((t.Arrival(Player.BodySize, World.Room.Solids()) + offset).OnPlane());
        Player.Velocity = (t.Outward * (t.IsHatch ? 10f : 7f)).OnPlane();
        Hold(Out(t.Side));
        Begin(phase, _current.TargetRoom);
    }

    private void Begin(Phase phase, string expected)
    {
        _phase = phase;
        _expected = expected;
        _arrived = false;
        _tick = 0;
        _standing = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_current.Room is null || Travel.Travelling)
        {
            return;
        }

        _tick++;
        switch (_phase)
        {
            case Phase.Crossing or Phase.Climbing or Phase.TurningBack or Phase.FallingBack when !_arrived:
                if (_tick > (_phase == Phase.TurningBack ? BackTicks : CrossTicks))
                {
                    Fail(_phase switch
                    {
                        Phase.TurningBack => "turning back at once, the move held back out never travelled back",
                        Phase.FallingBack => "falling straight back down the hatch just climbed never travelled back down",
                        _ => $"crossing {Place()} never travelled",
                    });
                }

                return;
            case Phase.Crossing or Phase.Climbing or Phase.TurningBack or Phase.FallingBack:
                Arrived();
                return;
            case Phase.Standing:
                Stand();
                return;
        }
    }

    /// <summary>Where along the opening the current crossing is.</summary>
    private string Place() => $"{_place.X:0.##} m across and {_place.Y:0.##} m up its opening (from the marker)";

    private void Arrived()
    {
        _arrived = false;
        Hold();
        if (World.RoomName != _expected)
        {
            Fail($"arrived in {World.RoomName}, not {_expected}");
            return;
        }

        if (Arrival() is { } arrival && arrival.Threshold.Crossed(Player.Body))
        {
            Fail($"arrived past the threshold of {arrival.Name}, at {Player.GlobalPosition}");
            return;
        }

        _crossings++;
        switch (_phase)
        {
            case Phase.Crossing:
                _places.RemoveAt(0);
                if (_places.Count > 0)
                {
                    Cross(_places[0], Phase.Crossing);
                }
                else
                {
                    Begin(Phase.Standing, _current.TargetRoom);
                }

                return;
            case Phase.Climbing:
                // Arrived up through the hatch: no speed and no steering, so it falls (or sinks) straight back down through it.
                Player.Velocity = Vector3.Zero;
                Player.Steer(0, 0f);
                Hold(InputActions.MoveDown);
                Begin(Phase.FallingBack, _current.Room);
                return;
            default:
                _back = false;
                Begin(Phase.Standing, _current.Room);
                return;
        }
    }

    /// <summary>The exit the pilgrim just arrived through, in the room it is in now.</summary>
    private RoomExit? Arrival() => World.Room.FindExit(World.RoomName == _current.TargetRoom ? _current.TargetExit : _current.Exit);

    private void Stand()
    {
        _standing = Player.IsOnFloor() ? _standing + 1 : 0;
        if (_standing < SettleTicks && _tick < LandTicks)
        {
            return;
        }

        if (World.RoomName != _expected)
        {
            Fail($"ended up in {World.RoomName}, not {_expected} (bounced back through the arrival exit?)");
            return;
        }

        if (!World.Map.Knows(_expected))
        {
            Fail($"{_expected} was not placed on the map");
            return;
        }

        if (!_back || Arrival() is not { } arrival)
        {
            NextExit();
            return;
        }

        switch (arrival.Side)
        {
            case ExitSide.West or ExitSide.East:
                Hold(Out(arrival.Side));
                Begin(Phase.TurningBack, _current.Room);
                return;
            case ExitSide.Down:
                Cross(NVector2.Zero, Phase.Climbing);
                return;
            default:
                NextExit(); // arrived falling through a roof: the way back up is the room check's to prove
                return;
        }
    }

    private void Fail(string problem)
    {
        _problems.Add($"{What}: {problem}");
        NextExit();
    }

    /// <summary>What a player holds to go out through an exit facing <paramref name="side"/>: the move out of a doorway, and down to sink.</summary>
    private static StringName[] Out(ExitSide side) => side switch
    {
        ExitSide.West => new[] { InputActions.MoveLeft, InputActions.MoveDown },
        ExitSide.East => new[] { InputActions.MoveRight, InputActions.MoveDown },
        ExitSide.Down => new[] { InputActions.MoveDown },
        _ => System.Array.Empty<StringName>(),
    };

    /// <summary>Holds exactly <paramref name="actions"/>, releasing what was held.</summary>
    private void Hold(params StringName[] actions)
    {
        foreach (StringName held in _held)
        {
            Input.ActionRelease(held);
        }

        _held.Clear();
        foreach (StringName action in actions)
        {
            Input.ActionPress(action);
            _held.Add(action);
        }
    }

    private void Finish()
    {
        foreach (string problem in _problems)
        {
            GD.PushError($"TRAVEL CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"TRAVEL OK: {_exits} exits, {_crossings} crossings{(Shard.Count > 1 ? $" (part {Shard.Index} of {Shard.Count})" : "")}");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
