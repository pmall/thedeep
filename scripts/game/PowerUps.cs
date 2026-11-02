using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The power-ups in play: walking up to a <see cref="PowerUp"/> takes it (the pilgrim holds still while the shard sinks in, then
/// its stat rises for good, <see cref="Player.Strengthen"/>, the game saves, and its name and what it does show). Taken ones lie
/// empty when their room loads. What is done for good is the session's to keep (<see cref="Session.Remember"/>); the rule is the
/// core's (<see cref="Prowess"/>).
/// </summary>
public partial class PowerUps : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public Session Session { get; set; } = null!;
    [Export] public Banner Banner { get; set; } = null!;

    /// <summary>How close (meters, on the play plane) the pilgrim must stand to take one.</summary>
    [Export] public float TakeRange { get; set; } = 1.2f;

    private bool _taking;

    /// <summary>Raised when a power-up is taken, once its stat has risen.</summary>
    public event System.Action<PowerUp>? Taken;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree() => World.RoomLoaded -= OnRoomLoaded;

    public override void _PhysicsProcess(double delta)
    {
        if (_taking || !Player.InControl)
        {
            return;
        }

        Vector3 p = Player.GlobalPosition;
        IReadOnlyList<PowerUp> found = World.Room.All<PowerUp>();
        for (int i = 0; i < found.Count; i++)
        {
            PowerUp power = found[i];
            if (!power.Taken && new Vector2(power.GlobalPosition.X - p.X, power.GlobalPosition.Y - p.Y).Length() <= TakeRange)
            {
                Take(power);
                return;
            }
        }
    }

    private async void Take(PowerUp power)
    {
        _taking = true;
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        await power.Sink(Player, new Vector3(0f, 1.05f, 0f));
        Player.Strengthen(Player.Prowess.With(power.Stat, power.Amount));
        Session.Remember(power.Name);
        Player.Frozen = false;
        _taking = false;
        Taken?.Invoke(power);
        await Banner.Show(PowerStats.NameLine(power.Stat), PowerStats.HintLine(power.Stat));
    }

    private void OnRoomLoaded(Room room)
    {
        foreach (PowerUp power in room.All<PowerUp>())
        {
            if (Session.Done(World.NameOf(room.SceneFilePath), power.Name))
            {
                power.Empty();
            }
        }
    }
}
