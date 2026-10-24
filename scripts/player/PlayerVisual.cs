using System;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// The pilgrim's body: a <see cref="PartBody"/> built from its part assembly, animated procedurally by
/// <see cref="PilgrimMotion"/> (run cycle with planted feet, leaps, dash lunge, leaning, landing
/// compression), its coat, braid and lantern flowing behind, turning smoothly through a glance at the
/// camera when the facing flips. Pivot is at the feet. <see cref="Player"/> calls <see cref="Animate"/>
/// after each physics move, so physics interpolation keeps it smooth.
/// </summary>
public partial class PlayerVisual : Node3D
{
    [Export] public PartBody Body { get; set; } = null!;

    /// <summary>The light the pilgrim carries; it moves into the lantern at the end of <see cref="LanternChain"/>.</summary>
    [Export] public OmniLight3D? CarriedLight { get; set; }

    [Export] public StringName LanternChain { get; set; } = "Lantern";

    /// <summary>How fast the body turns around when the facing flips, 1/s (higher is snappier).</summary>
    [Export] public float TurnSharpness { get; set; } = 20f;

    private HumanoidRig? _rig;
    private PilgrimMotion? _motion;
    private int _facing = 1;
    private float _yaw;

    /// <summary>A foot was planted at this world position, on the near (+1) or far (-1) side.</summary>
    public event Action<Vector3, int>? Footfall;

    /// <summary>How much the run cycle shows, 0 standing to 1 at run speed.</summary>
    public float Gait => _motion?.Gait ?? 0f;

    public override void _Ready()
    {
        _rig = HumanoidRig.From(Body);
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

    public void SetFacing(int facing) => _facing = facing < 0 ? -1 : 1;

    /// <summary>Kicks the landing compression; <paramref name="impactSpeed"/> is the downward speed at touchdown.</summary>
    public void Land(float impactSpeed) => _motion?.Land(impactSpeed);

    /// <summary>One physics tick of animation, after the body moved.</summary>
    public void Animate(float delta, Vector3 velocity, MotionMode mode, bool onFloor, float floorY)
    {
        _yaw = Motion.Damp(_yaw, TargetYaw, TurnSharpness, delta);
        Basis = new Basis(Vector3.Up, _yaw);
        if (_rig is null || _motion is null)
        {
            return;
        }

        _motion.Update(delta, new NVector2(velocity.X, velocity.Y), _facing, mode);
        _rig.Apply(_motion.Pose);
        Body.StepChains(delta, onFloor ? floorY + 0.02f : float.NaN);

        if (_motion.Footfall != 0 && Body.Joint(_motion.Footfall > 0 ? "FootR" : "FootL") is { } foot)
        {
            Footfall?.Invoke(foot.GlobalPosition with { Y = floorY }, _motion.Footfall);
        }
    }

    /// <summary>Snaps to a still stance facing the current way (spawn, teleport).</summary>
    public void Snap()
    {
        _yaw = TargetYaw;
        Basis = new Basis(Vector3.Up, _yaw);
        if (_rig is not null && _motion is not null)
        {
            _motion.Reset();
            _rig.Apply(_motion.Pose);
            Body.ResetChains();
        }

        ResetPhysicsInterpolation();
    }

    /// <summary>Facing +X is yaw 0; facing -X is -π, so a turn either way passes through facing the camera.</summary>
    private float TargetYaw => _facing > 0 ? 0f : -Mathf.Pi;
}
