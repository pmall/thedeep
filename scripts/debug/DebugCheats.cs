using Godot;

namespace TheDeep;

/// <summary>
/// Debug keys for playtesting: R / pad Back respawns, F1–F5 toggle each ability, F6 cycles the
/// room's biome through the world's layers, F9 cycles the paint (the biome's own, each of <see cref="PaintVariants"/>,
/// none) to compare the signature's variants and the raw render. Removed from release builds.
/// </summary>
public partial class DebugCheats : Node
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public World World { get; set; } = null!;

    /// <summary>The rooms' painted pass (the shared resources/stage/paint_pass.tres).</summary>
    [Export] public PaintPass Paint { get; set; } = null!;

    /// <summary>The styles F9 cycles through after the biome's own.</summary>
    [Export] public PaintStyle[] PaintVariants { get; set; } = System.Array.Empty<PaintStyle>();

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

        WorldLayer[] layers = World.Definition.Layers;
        if (@event.IsActionPressed(InputActions.DebugCycleBiome) && layers.Length > 0)
        {
            int next = (System.Array.FindIndex(layers, l => l.Biome == World.Room.Biome) + 1) % layers.Length;
            World.Room.ApplyBiome(layers[next].Biome);
            GD.Print($"[debug] biome: {layers[next].Biome.DisplayName}");
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(InputActions.DebugCyclePaint))
        {
            CyclePaint();
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

    /// <summary>The biome's own paint, then each variant, then none, then the biome's own again.</summary>
    private void CyclePaint()
    {
        if (!Paint.Enabled)
        {
            Paint.Enabled = true;
            Paint.Override = null;
        }
        else if (Paint.Override is null)
        {
            Paint.Override = PaintVariants.Length > 0 ? PaintVariants[0] : null;
            Paint.Enabled = PaintVariants.Length > 0;
        }
        else
        {
            int next = System.Array.IndexOf(PaintVariants, Paint.Override) + 1;
            Paint.Override = next < PaintVariants.Length ? PaintVariants[next] : null;
            Paint.Enabled = next < PaintVariants.Length;
        }

        string name = !Paint.Enabled ? "none" : Paint.Style?.DisplayName ?? "none";
        GD.Print($"[debug] paint: {name}{(Paint.Enabled && Paint.Override is null ? " (the biome's)" : "")}");
    }
}
