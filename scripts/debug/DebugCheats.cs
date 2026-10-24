using Godot;

namespace TheDeep;

/// <summary>
/// Debug keys for playtesting: R / pad Back respawns, F1–F5 toggle each ability, F6 cycles the
/// room's biome profile. Removed from release builds.
/// </summary>
public partial class DebugCheats : Node
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public World World { get; set; } = null!;

    /// <summary>Profiles F6 cycles through.</summary>
    [Export] public BiomeProfile[] Biomes { get; set; } = System.Array.Empty<BiomeProfile>();

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
            Player.Respawn(World.Room.SpawnPoint.GlobalPosition);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(InputActions.DebugCycleBiome) && Biomes.Length > 0)
        {
            int next = (System.Array.IndexOf(Biomes, World.Room.Biome) + 1) % Biomes.Length;
            World.Room.ApplyBiome(Biomes[next]);
            GD.Print($"[debug] biome: {Biomes[next].DisplayName}");
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
