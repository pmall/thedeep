using Godot;

namespace TheDeep;

/// <summary>
/// Dressing that turns (visual only): spins its node about <see cref="Axis"/> at <see cref="Speed"/> radians per second,
/// from <see cref="Phase"/>. The core's old machinery, still turning. Moved on physics ticks with interpolation, so look-dev
/// runs at a fixed frame rate catch it in the same pose every time.
/// </summary>
public partial class Spinner : Node3D
{
    [Export] public Vector3 Axis { get; set; } = Vector3.Back;
    [Export] public float Speed { get; set; } = 0.2f;
    [Export] public float Phase { get; set; }

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

    private void Apply() => Basis = new Basis(Axis.Normalized(), _angle) * _rest;
}
