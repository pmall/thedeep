using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Moves the player between rooms through their <see cref="RoomExit"/> markers: walking into an open
/// exit holds the player still under a short fade, loads the target room, places the player at the
/// arrival exit with its momentum kept (<see cref="ExitMath"/>), snaps the camera and fades back in.
/// The arrival exit is disarmed until the player leaves it, so arriving never bounces straight back.
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

    private bool _travelling;

    /// <summary>Raised after the player arrived in a new room through an exit.</summary>
    public event System.Action<RoomExit>? Travelled;

    public bool Travelling => _travelling;

    /// <summary>False keeps the player in the room whatever exit they walk into (look-dev runs stay in their room).</summary>
    public bool Enabled { get; set; } = true;

    public override void _Ready()
    {
        World.RoomLoaded += Listen;
        Listen(World.Room);
    }

    public override void _ExitTree()
    {
        World.RoomLoaded -= Listen;
    }

    private void Listen(Room room)
    {
        foreach (RoomExit exit in room.Exits())
        {
            exit.Crossed += OnCrossed;
        }
    }

    private async void OnCrossed(RoomExit exit)
    {
        if (!Enabled || _travelling || !ResourceLoader.Exists(exit.TargetRoom))
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
        NVector2 from = ToN(exit.RoomPosition);
        NVector2 player = ToN(new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Y));
        NVector2 velocity = ToN(new Vector2(Player.Velocity.X, Player.Velocity.Y));
        MapRoom? here = World.Map.Find(World.RoomName);
        NVector2 hereOffset = here?.Offset ?? NVector2.Zero;
        string targetExit = exit.TargetExit;

        // The arrival exit's position is only known once the room is loaded: place the room on the map after.
        Room room = World.Load(GD.Load<PackedScene>(exit.TargetRoom), map: false);
        RoomExit? arrival = room.FindExit(targetExit);
        if (arrival is null)
        {
            GD.PushError($"{exit.TargetRoom} has no exit named {targetExit}; arriving at its spawn point");
            World.Enter(room, hereOffset);
            Player.Respawn(room.SpawnPoint.GlobalPosition);
            return null;
        }

        NVector2 to = ToN(arrival.RoomPosition);
        World.Enter(room, ExitMath.RoomOffset(hereOffset, from, to));
        NVector2 position = ExitMath.Arrival(player, from, to, ToN(arrival.Size));
        NVector2 keep = ExitMath.ArrivalVelocity(velocity, ToN(arrival.Size), ClimbRise * Player.Tuning.JumpTakeOffSpeed);
        arrival.Disarm();
        Player.Arrive(new Vector3(position.X, position.Y, 0f), new Vector3(keep.X, keep.Y, 0f));
        return arrival;
    }

    private static NVector2 ToN(Vector2 v) => new(v.X, v.Y);
}
