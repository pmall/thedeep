using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Moves the player between rooms through their <see cref="RoomExit"/> markers. Every physics tick, a body touching an open exit's
/// trigger (<see cref="Threshold.Trigger"/>: the whole opening, out past the room's edge) leaves through it: the player is held still
/// under a short fade, the target room is loaded, the player is placed clear of the arrival exit's trigger with its speed kept
/// (<see cref="Threshold.Arrival"/>), the camera snaps and the fade lifts. Every crossing is written to the log (`[travel]`).
/// </summary>
public partial class RoomTravel : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public ScreenFade Fade { get; set; } = null!;

    /// <summary>Fade to the veil and back, seconds: short, so crossing a doorway feels like a breath, not a load.</summary>
    [Export] public float FadeOutTime { get; set; } = 0.14f;
    [Export] public float FadeInTime { get; set; } = 0.32f;

    /// <summary>Upward speed kept at least when climbing up through a hatch, as a share of the jump's take-off speed.</summary>
    [Export] public float ClimbRise { get; set; } = 0.85f;

    /// <summary>How long an upward arrival carries the player sideways onto the floor (it ends on landing).</summary>
    [Export] public float SteerTime { get; set; } = 0.9f;

    private bool _travelling;

    /// <summary>Raised after the player arrived in a new room through an exit.</summary>
    public event System.Action<RoomExit>? Travelled;

    /// <summary>A room transition is under way.</summary>
    public bool Travelling => _travelling;

    /// <summary>False keeps the player in the room whatever exit they walk into (look-dev runs stay in their room).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The exit the player came into the current room through, or null (it woke there, or started there).</summary>
    public RoomExit? Entry { get; private set; }

    public override void _Ready()
    {
        World.RoomLoaded += _ => Entry = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Enabled || _travelling || Player.Frozen)
        {
            return;
        }

        Box body = Player.Body;
        System.Collections.Generic.IReadOnlyList<RoomExit> exits = World.Room.All<RoomExit>();
        for (int i = 0; i < exits.Count; i++)
        {
            if (exits[i].Open && exits[i].Threshold.Crossed(body))
            {
                Cross(exits[i]);
                return;
            }
        }
    }

    private async void Cross(RoomExit exit)
    {
        if (!ResourceLoader.Exists(exit.TargetRoom))
        {
            return;
        }

        _travelling = true;
        Player.Frozen = true;
        await Fade.To(1f, FadeOutTime);
        RoomExit? arrival = Go(exit);

        // Building a room takes a long frame; let it pass under the veil so the fade-in starts smooth.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Player.Frozen = false;
        _travelling = false;
        if (arrival is not null)
        {
            Travelled?.Invoke(arrival);
        }

        await Fade.To(0f, FadeInTime);
    }

    /// <summary>Loads the exit's target room and places the player at its arrival exit. Returns the arrival exit.</summary>
    public RoomExit? Go(RoomExit exit)
    {
        NVector2 from = exit.RoomPosition.ToCore();
        NVector2 player = Player.GlobalPosition.Flat();
        NVector2 velocity = Player.Velocity.Flat();
        MapRoom? here = World.Map.Find(World.RoomName);
        NVector2 hereOffset = here?.Offset ?? NVector2.Zero;
        string targetExit = exit.TargetExit;
        string leaving = World.RoomName, leavingExit = exit.Name, target = exit.TargetRoom;

        // The arrival exit's position is only known once the room is loaded: place the room on the map after.
        Room room = World.Load(World.Scene(target), map: false);
        RoomExit? arrival = room.FindExit(targetExit);
        if (arrival is null)
        {
            GD.PushError($"{target} has no exit named {targetExit}; arriving at its spawn point");
            World.Enter(room, hereOffset);
            Player.Respawn(room.SpawnPoint.GlobalPosition);
            return null;
        }

        Threshold to = arrival.Threshold;
        World.Enter(room, ExitMath.RoomOffset(hereOffset, from, to.Marker));
        World.Map.Take(leaving, leavingExit);
        World.Map.Take(World.RoomName, arrival.Name);
        NVector2 position = to.Arrival(Player.BodySize, room.Solids(), along: player.X - from.X);
        NVector2 keep = to.ArrivalVelocity(velocity, ClimbRise * Player.Tuning.JumpTakeOffSpeed);
        GD.Print($"[travel] {leaving}:{leavingExit} → {World.RoomName}:{arrival.Name}, from {player} moving {velocity} ({Player.CurrentState.GetType().Name}), to {position} moving {keep}");
        Player.Arrive(position.OnPlane(), keep.OnPlane());
        Entry = arrival;
        if (arrival.ArrivalSide != 0 && to.Side == ExitSide.Down)
        {
            Player.Steer(arrival.ArrivalSide, SteerTime);
        }

        return arrival;
    }

    /// <summary>
    /// Puts the player back where it came into the room (it fell out of it: a hole the room check missed), or returns false if it came
    /// in some other way (waking at a shrine, starting there).
    /// </summary>
    public bool Return()
    {
        if (Entry is not { } entry || !IsInstanceValid(entry))
        {
            return false;
        }

        Player.Respawn(entry.Threshold.Arrival(Player.BodySize, World.Room.Solids()).OnPlane());
        return true;
    }
}
