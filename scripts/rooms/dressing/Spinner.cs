using Godot;

namespace TheDeep;

/// <summary>
/// Dressing that turns (visual only): spins its node about <see cref="Axis"/> at <see cref="Speed"/> radians per second,
/// from <see cref="Phase"/>. The core's old machinery, still turning; a power-up's crystals and rings. The axis is in the node's
/// own frame, as the scene places it: a wheel or ring (a <see cref="RingMesh"/> is a band round its Y) turns in its own plane
/// about (0, 1, 0), and a tilted one keeps its tilt. The room check holds all a spinner sweeps in a turn off the play plane
/// (<see cref="SpinSweep"/>). Moved on physics ticks with interpolation, so look-dev runs at a fixed frame rate catch it in the
/// same pose every time.
/// </summary>
public partial class Spinner : Node3D
{
    /// <summary>The axis it turns about, in its own frame (its rest pose's): any length but zero.</summary>
    [Export] public Vector3 Axis { get; set; } = Vector3.Back;

    [Export] public float Speed { get; set; } = 0.2f;
    [Export] public float Phase { get; set; }

    /// <summary>The axis it turns about, in world space (turning about it leaves it where it is).</summary>
    public Vector3 GlobalAxis => (GlobalBasis * Axis).Normalized();

    private Basis _rest;
    private float _angle;

    public override void _Ready()
    {
        _rest = Basis;
        _angle = Phase;
        Apply();
    }

    public override void _PhysicsProcess(double delta)
    {
        _angle += Speed * (float)delta;
        Apply();
    }

    private void Apply() => Basis = _rest * new Basis(Axis.Normalized(), _angle);
}
