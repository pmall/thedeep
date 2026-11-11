using System;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// The pilgrim's body: a <see cref="PartBody"/> built from its part assembly, animated procedurally by
/// <see cref="PilgrimMotion"/> (run cycle with planted feet, leaps, dash lunge, leaning, landing
/// compression; clinging on Mag-grip, swimming, the dive's spin, the harpoon's reach, strikes, the recoil when hurt and the
/// ledge vault), its coat, braid and lantern flowing behind, turning smoothly through a glance at the
/// camera when the facing flips. A strike draws the blade out of the near forearm (the part on <see cref="BladeJoint"/>, slid out
/// along its length) and its sweep leaves the <see cref="Slash"/>. Pivot is at the feet. <see cref="Player"/> calls <see cref="Animate"/>
/// after each physics move, so physics interpolation keeps it smooth.
/// </summary>
public partial class PlayerVisual : Node3D
{
    [Export] public PartBody Body { get; set; } = null!;

    /// <summary>The light the pilgrim carries; it moves into the lantern at the end of <see cref="LanternChain"/>.</summary>
    [Export] public OmniLight3D? CarriedLight { get; set; }

    [Export] public StringName LanternChain { get; set; } = "Lantern";

    /// <summary>The joint the blade part hangs from (down its -Y axis), on the near forearm.</summary>
    [Export] public StringName BladeJoint { get; set; } = "Blade";

    /// <summary>The slash the blade's sweep leaves, or none.</summary>
    [Export] public SlashTrail? Slash { get; set; }

    /// <summary>How fast the body turns around when the facing flips, 1/s (higher is snappier).</summary>
    [Export] public float TurnSharpness { get; set; } = 20f;

    /// <summary>How fast the body eases into a new pose after the collider snapped (a Mag-grip corner), 1/s.</summary>
    [Export] public float CarrySharpness { get; set; } = 16f;

    private HumanoidRig? _rig;
    private Node3D? _blade;
    private Node3D? _shoulder;
    private float _bladeLength;
    private PilgrimMotion? _motion;
    private int _facing = 1;
    private float _yaw;
    private int _blinkTime;
    private float _shimmer;

    /// <summary>Where the visible body still is relative to the collider after a snap; eases back to identity.</summary>
    private Transform3D _carry = Transform3D.Identity;

    /// <summary>A foot was planted at this world position, on the near (+1) or far (-1) side.</summary>
    public event Action<Vector3, int>? Footfall;

    /// <summary>How much the run cycle shows, 0 standing to 1 at run speed.</summary>
    public float Gait => _motion?.Gait ?? 0f;

    public override void _Ready()
    {
        _rig = HumanoidRig.From(Body);
        _shoulder = Body.Joint(nameof(HumanoidBone.UpperArmR));
        _blade = Body.Joint(BladeJoint);
        if (_blade is not null)
        {
            // The blade's length is its part's: whatever mesh the assembly hangs there.
            foreach (Node child in _blade.GetChildren())
            {
                if (child is MeshInstance3D part && part.Mesh is not null)
                {
                    _bladeLength = Mathf.Max(_bladeLength, part.Mesh.GetAabb().Size.Y);
                }
            }

            ShowBlade(0f);
        }
        if (CarriedLight is not null && Body.ChainEnd(LanternChain) is { } lantern)
        {
            Vector3 offset = CarriedLight.Position;
            CarriedLight.Reparent(lantern, false);
            CarriedLight.Position = offset;
        }
    }

    /// <summary>Sizes the animation for the player's run speed (the gait's stride scales with it).</summary>
    public void Setup(float runSpeed)
    {
        if (_rig is not null)
        {
            _motion = new PilgrimMotion(_rig.Dims, runSpeed);
        }
    }

    private static readonly StringName HurtParameter = "hurt";

    /// <summary>A pale shimmer over the body while the pilgrim can't be hurt (after a hit); false clears it. Per physics tick.</summary>
    public void Blink(bool on)
    {
        _blinkTime = on ? _blinkTime + 1 : 0;
        float shimmer = on ? 0.05f + 0.05f * Mathf.Sin(_blinkTime * 0.45f) : 0f;
        if (on || _blinkTime == 0 && _shimmer != 0f)
        {
            Body.SetShaderParameter(HurtParameter, shimmer);
        }

        _shimmer = shimmer;
    }

    public void SetFacing(int facing) => _facing = facing < 0 ? -1 : 1;

    /// <summary>A strike under way (<see cref="PilgrimMotion.Strike"/>); each physics tick it lasts.</summary>
    public void Strike(float progress, float windUpEnd, float activeEnd, StrikeKind kind) =>
        _motion?.Strike(progress, windUpEnd, activeEnd, kind);

    public void EndStrike() => _motion?.EndStrike();

    /// <summary>The held moment the body plays (resting, taking a relic, dying); None lets it go.</summary>
    public PilgrimMoment Moment
    {
        get => _motion?.Moment ?? PilgrimMoment.None;
        set
        {
            if (_motion is not null)
            {
                _motion.Moment = value;
            }
        }
    }

    /// <summary>A blow landed on the pilgrim: the body recoils.</summary>
    public void Hurt() => _motion?.Hurt();

    /// <summary>The ledge assist lifted the body onto a top: it vaults the edge.</summary>
    public void Vault() => _motion?.Vault();

    /// <summary>Where the Harpoon's line pulls, in the world: the near arm reaches along it while on the line.</summary>
    public void Reach(Vector3 toward)
    {
        if (_motion is not null)
        {
            Vector3 local = GetParentNode3D().GlobalBasis.Inverse() * toward;
            _motion.Reach = new NVector2(local.X * _facing, local.Y);
        }
    }

    /// <summary>Kicks the landing compression; <paramref name="impactSpeed"/> is the downward speed at touchdown.</summary>
    public void Land(float impactSpeed) => _motion?.Land(impactSpeed);

    /// <summary>
    /// The body (the parent) just snapped to a new pose: the visible body starts from
    /// <paramref name="previousGlobal"/> and eases into the new pose instead of jumping.
    /// </summary>
    public void CarryOver(Transform3D previousGlobal)
    {
        _carry = GetParentNode3D().GlobalTransform.AffineInverse() * previousGlobal * new Transform3D(new Basis(Vector3.Up, _yaw), Vector3.Zero).AffineInverse();
        Transform = _carry * new Transform3D(new Basis(Vector3.Up, _yaw), Vector3.Zero);
        ResetPhysicsInterpolation();
    }

    /// <summary>
    /// One physics tick of animation, after the body moved. <paramref name="velocity"/> is in the body's own
    /// frame; <paramref name="floorY"/> is NaN when the body isn't upright (Mag-grip).
    /// </summary>
    public void Animate(float delta, Vector3 velocity, MotionMode mode, bool onFloor, float floorY)
    {
        _yaw = Motion.Damp(_yaw, TargetYaw, TurnSharpness, delta);
        float k = 1f - Mathf.Exp(-CarrySharpness * delta);
        _carry = new Transform3D(new Basis(_carry.Basis.GetRotationQuaternion().Slerp(Quaternion.Identity, k)), _carry.Origin.Lerp(Vector3.Zero, k));
        if (_rig is null || _motion is null)
        {
            Transform = _carry * new Transform3D(new Basis(Vector3.Up, _yaw), Vector3.Zero);
            return;
        }

        _motion.Update(delta, velocity.Flat(), _facing, mode);
        Transform = _carry * new Transform3D(new Basis(Vector3.Up, _yaw + _motion.Spin), Vector3.Zero);
        _rig.Apply(_motion.Pose);
        ShowBlade(_motion.Blade);
        if (Slash is not null && _blade is not null && _shoulder is not null)
        {
            Slash.Track(_shoulder, _blade, _bladeLength, _motion.Edge * _motion.Blade, GlobalPosition, delta);
        }

        Body.StepChains(delta, onFloor && !float.IsNaN(floorY) ? floorY + 0.02f : float.NaN);

        if (_motion.Footfall != 0 && Body.Joint(_motion.Footfall > 0 ? "FootR" : "FootL") is { } foot)
        {
            Footfall?.Invoke(float.IsNaN(floorY) ? foot.GlobalPosition : foot.GlobalPosition with { Y = floorY }, _motion.Footfall);
        }
    }

    /// <summary>Snaps to a still stance facing the current way (spawn, teleport).</summary>
    public void Snap()
    {
        _yaw = TargetYaw;
        _carry = Transform3D.Identity;
        Transform = new Transform3D(new Basis(Vector3.Up, _yaw), Vector3.Zero);
        if (_rig is not null && _motion is not null)
        {
            _motion.Reset();
            _rig.Apply(_motion.Pose);
            ShowBlade(0f);
            Body.ResetChains();
        }

        Slash?.Clear();

        ResetPhysicsInterpolation();
    }

    /// <summary>The blade slid <paramref name="out"/> of the forearm (0 folded away, hidden; 1 all out), narrowing as it folds.</summary>
    private void ShowBlade(float @out)
    {
        if (_blade is null)
        {
            return;
        }

        _blade.Visible = @out > 0.02f;
        _blade.Scale = new Vector3(Mathf.Lerp(0.4f, 1f, @out), Mathf.Max(@out, 0.01f), 1f);
    }

    /// <summary>Facing +X is yaw 0; facing -X is -π, so a turn either way passes through facing the camera.</summary>
    private float TargetYaw => _facing > 0 ? 0f : -Mathf.Pi;
}
