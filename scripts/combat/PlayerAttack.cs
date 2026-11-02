using System;
using Godot;

namespace TheDeep;

/// <summary>
/// The pilgrim's strikes (the attack action): a quick crescent, timed by <see cref="Swing"/> from the tuning (wind-up, a hit window,
/// recovery). Which strike a press gives is the core's <see cref="Combo"/> (a chain of two swings and a finisher, a down strike in
/// the air, a dash strike); each one's reach and blow are its <see cref="StrikeShape"/>. It doesn't stop the pilgrim: momentum
/// carries through. While the window is open it raises <see cref="Striking"/> with the area it covers each tick; whoever owns
/// targets (the machine director) resolves the hits, once per target per strike. Works upright and on Mag-grip surfaces (the area
/// follows the body).
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
    private Combo? _combo;
    private ShaderMaterial? _material;
    private SwingPhase _lastPhase = SwingPhase.Ready;
    private Vector3 _slashRest;

    // From the exports, on first use (they are set before the node enters the tree).
    private Swing Timing => _swing ??= new Swing(Player.Tuning.AttackWindUp, Player.Tuning.AttackActive, Player.Tuning.AttackRecover,
        Player.Tuning.AttackCooldown);

    private Combo Chain => _combo ??= Player.Tuning.NewCombo();

    private ShaderMaterial SlashMaterial => _material ??= (ShaderMaterial)Slash.MaterialOverride;
    private int _facing = 1;

    /// <summary>The hit window is open: the area it covers on the play plane, which strike this is (a count), and its shape.</summary>
    public event Action<Rect2, int, StrikeShape>? Striking;

    /// <summary>A strike started, toward <see cref="Facing"/>.</summary>
    public event Action<StrikeShape>? Swung;

    /// <summary>The strike under way (or the last one).</summary>
    public StrikeShape Strike { get; private set; }

    public int Facing => _facing;

    public SwingPhase Phase => Timing.Phase;

    public override void _Ready()
    {
        Slash.Mesh ??= new SlashMesh();
        Slash.Visible = false;
        _slashRest = Slash.Position;
        Player.Respawned += OnRespawned;
    }

    public override void _ExitTree()
    {
        Player.Respawned -= OnRespawned;
    }

    private void OnRespawned()
    {
        Timing.Cancel();
        Chain.Reset();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player.HitStopped)
        {
            return; // the blow holds: the window stays open as long as the stop lasts
        }

        float dt = (float)delta;
        Chain.Tick(dt);
        bool started = false;
        if (Player.InControl && Input.IsActionJustPressed(InputActions.Attack))
        {
            started = Timing.Press();
        }

        started |= Timing.Tick(dt);
        if (started)
        {
            _facing = Player.Facing;
            Strike = Chain.Next(Player.CurrentState is States.DashState, !Player.IsOnFloor(), Player.CurrentInput.MoveY < 0);
            Swung?.Invoke(Strike);
        }

        SwingPhase phase = Timing.Phase;
        if (phase == SwingPhase.Ready && _lastPhase != SwingPhase.Ready)
        {
            Chain.Ended();
        }

        _lastPhase = phase;
        if (phase == SwingPhase.Active)
        {
            Striking?.Invoke(Area(), Timing.Count, Strike);
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
        bool down = Strike.Toward.Y < 0f;
        float size = Strike.Reach / 1.8f;
        Slash.Scale = new Vector3(_facing * size, size, 1f);
        Slash.Rotation = new Vector3(0f, 0f, down ? -Mathf.Pi * 0.5f * _facing : 0f);
        Slash.Position = down ? new Vector3(0f, _slashRest.Y - 1.1f, _slashRest.Z) : _slashRest with { X = Mathf.Abs(_slashRest.X) * _facing };
        SlashMaterial.SetShaderParameter(ProgressParameter, p);
        SlashMaterial.SetShaderParameter(StrengthParameter, 1f - Mathf.SmoothStep(0.7f, 1f, p));
    }

    /// <summary>
    /// The area the strike covers now, on the play plane: in front of the chest along the body's facing, or for a down strike under
    /// the feet, from the hips down.
    /// </summary>
    public Rect2 Area()
    {
        Vector3 up = Player.GlobalBasis.Y;
        bool down = Strike.Toward.Y < 0f;
        Vector3 along = down ? -up : Player.GlobalBasis.X * _facing;
        Vector3 from = Player.GlobalPosition + up * (down ? 0.6f : ChestHeight);
        Vector3 center = from + along * (Strike.Reach * 0.5f + (down ? 0f : 0.1f));
        bool horizontal = Mathf.Abs(along.X) > 0.5f;
        var size = horizontal ? new Vector2(Strike.Reach, Strike.Width) : new Vector2(Strike.Width, Strike.Reach);
        return new Rect2(new Vector2(center.X, center.Y) - size * 0.5f, size);
    }
}
