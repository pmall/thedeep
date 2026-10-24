using Godot;

namespace TheDeep;

/// <summary>
/// Debug keys for playtesting: R / pad Back respawns, F1–F5 toggle each ability.
/// Removed from release builds.
/// </summary>
public partial class DebugCheats : Node
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public Room Room { get; set; } = null!;

    private (StringName Action, Ability Ability)[] _toggles = null!;

    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        _toggles = new[]
        {
            (InputActions.DebugToggleMagGrip, Ability.MagGrip),
            (InputActions.DebugToggleHarpoon, Ability.Harpoon),
            (InputActions.DebugToggleDrill, Ability.Drill),
            (InputActions.DebugToggleBallast, Ability.Ballast),
            (InputActions.DebugToggleHeatPlating, Ability.HeatPlating),
        };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.DebugRespawn))
        {
            Player.Respawn(Room.SpawnPoint.GlobalPosition);
            GetViewport().SetInputAsHandled();
            return;
        }

        foreach (var (action, ability) in _toggles)
        {
            if (@event.IsActionPressed(action))
            {
                Player.Abilities.Toggle(ability);
                GD.Print($"[debug] {ability}: {(Player.Abilities.Has(ability) ? "unlocked" : "locked")}");
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }
}
