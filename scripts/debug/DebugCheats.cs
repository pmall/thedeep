using Godot;

namespace TheDeep;

/// <summary>
/// Debug keys for playtesting: R / pad Back respawns, F1–F5 toggle each ability, F6 cycles the
/// room's biome through the world's layers, F9 cycles the drawn looks (<see cref="ToonLook"/>). Removed from release builds.
/// </summary>
public partial class DebugCheats : Node
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public World World { get; set; } = null!;

    /// <summary>The drawn look, cycled by F9, or none.</summary>
    [Export] public ToonLook? Look { get; set; }

    private static readonly (StringName Action, Ability Ability)[] Toggles =
    {
        (InputActions.DebugToggleMagGrip, Ability.MagGrip),
        (InputActions.DebugToggleHarpoon, Ability.Harpoon),
        (InputActions.DebugToggleDrill, Ability.Drill),
        (InputActions.DebugToggleBallast, Ability.Ballast),
        (InputActions.DebugToggleHeatPlating, Ability.HeatPlating),
    };

    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.DebugRespawn))
        {
            Player.Respawn(World.Room.SpawnPoint.GlobalPosition);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(InputActions.DebugToggleLook) && Look is not null)
        {
            Look.Cycle();
            GD.Print($"[debug] look: {Look.Current?.Name ?? "plain"}");
            GetViewport().SetInputAsHandled();
            return;
        }

        WorldLayer[] layers = World.Definition.Layers;
        if (@event.IsActionPressed(InputActions.DebugCycleBiome) && layers.Length > 0)
        {
            int next = (System.Array.FindIndex(layers, l => l.Biome == World.Room.Biome) + 1) % layers.Length;
            World.Room.ApplyBiome(layers[next].Biome);
            Look?.StyleRoom(World.Room);
            GD.Print($"[debug] biome: {layers[next].Biome.DisplayName}");
            GetViewport().SetInputAsHandled();
            return;
        }

        foreach (var (action, ability) in Toggles)
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
