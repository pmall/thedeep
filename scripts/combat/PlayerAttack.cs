using System;
using Godot;

namespace TheDeep;

/// <summary>
/// The pilgrim's swing (the attack action): a quick crescent in front of it, timed by <see cref="Swing"/>
/// from the tuning (wind-up, a hit window, recovery). It doesn't stop the pilgrim: momentum carries through.
/// While the window is open it raises <see cref="Striking"/> with the area it covers each tick; whoever owns
/// targets (the machine director) resolves the hits, once per target per swing. Works upright and on
/// Mag-grip surfaces (the area follows the body).
/// </summary>
public partial class PlayerAttack : Node3D
{
    private static readonly StringName ProgressParameter = "progress";
    private static readonly StringName StrengthParameter = "strength";

    [Export] public Player Player { get; set; } = null!;

    /// <summary>The crescent drawn by a swing (slash.tres; a <see cref="SlashMesh"/> unless the scene gives it a mesh); mirrored to the facing.</summary>
    [Export] public MeshInstance3D Slash { get; set; } = null!;

    /// <summary>Height of the chest, where a swing is centered, above the feet.</summary>
    [Export] public float ChestHeight { get; set; } = 1.05f;

    private Swing? _swing;
    private ShaderMaterial? _material;

    // From the exports, on first use (they are set before the node enters the tree).
    private Swing Timing => _swing ??= new Swing(Player.Tuning.AttackWindUp, Player.Tuning.AttackActive, Player.Tuning.AttackRecover,
        Player.Tuning.AttackCooldown);

    private ShaderMaterial SlashMaterial => _material ??= (ShaderMaterial)Slash.MaterialOverride;
    private int _facing = 1;

    /// <summary>The hit window is open: the area it covers on the play plane, and which swing this is.</summary>
    public event Action<Rect2, int>? Striking;

    /// <summary>A swing started, toward <see cref="Facing"/>.</summary>
    public event Action? Swung;

    public int Facing => _facing;

    public SwingPhase Phase => Timing.Phase;

    public override void _Ready()
    {
        Slash.Mesh ??= new SlashMesh();
        Slash.Visible = false;
        Player.Respawned += Timing.Cancel;
    }

    public override void _ExitTree()
    {
        Player.Respawned -= Timing.Cancel;
    }

    public override void _PhysicsProcess(double delta)
    {
        bool started = false;
        if (Player.InControl && Input.IsActionJustPressed(InputActions.Attack))
        {
            started = Timing.Press();
        }

        started |= Timing.Tick((float)delta);
        if (started)
        {
            _facing = Player.Facing;
            Swung?.Invoke();
        }

        if (Timing.Phase == SwingPhase.Active)
        {
            Striking?.Invoke(Area(), Timing.Count);
        }
    }

    public override void _Process(double delta)
    {
        bool on = Timing.Phase != SwingPhase.Ready;
        Slash.Visible = on;
        if (!on)
        {
            return;
        }

        float p = Timing.Progress;
        Slash.Scale = new Vector3(_facing, 1f, 1f);
        Slash.Position = Slash.Position with { X = Mathf.Abs(Slash.Position.X) * _facing };
        SlashMaterial.SetShaderParameter(ProgressParameter, p);
        SlashMaterial.SetShaderParameter(StrengthParameter, 1f - Mathf.SmoothStep(0.7f, 1f, p));
    }

    /// <summary>The area a swing covers now: in front of the chest along the body's facing, on the play plane.</summary>
    public Rect2 Area()
    {
        PlayerTuning t = Player.Tuning;
        Vector3 up = Player.GlobalBasis.Y;
        Vector3 forward = Player.GlobalBasis.X * _facing;
        Vector3 center = Player.GlobalPosition + up * ChestHeight + forward * (t.AttackReach * 0.5f + 0.1f);
        bool horizontal = Mathf.Abs(forward.X) > 0.5f;
        var size = horizontal ? new Vector2(t.AttackReach, t.AttackHeight) : new Vector2(t.AttackHeight, t.AttackReach);
        return new Rect2(new Vector2(center.X, center.Y) - size * 0.5f, size);
    }
}
