using Godot;

namespace TheDeep;

/// <summary>
/// The player's placeholder body: turns to face the movement direction and does a little
/// squash and stretch. Pivot is at the feet. Updated on physics ticks so physics interpolation stays smooth.
/// </summary>
public partial class PlayerVisual : Node3D
{
    /// <summary>How fast squash/stretch returns to normal, in 1/s.</summary>
    [Export] public float SquashRecovery { get; set; } = 14f;

    private Vector2 _squash = Vector2.One;
    private int _facing = 1;

    public void SetFacing(int facing) => _facing = facing < 0 ? -1 : 1;

    public void Stretch() => _squash = new Vector2(0.8f, 1.25f);

    public void Squash() => _squash = new Vector2(1.25f, 0.78f);

    public void DashStretch() => _squash = new Vector2(1.35f, 0.8f);

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;
        _squash = new Vector2(
            Motion.Damp(_squash.X, 1f, SquashRecovery, d),
            Motion.Damp(_squash.Y, 1f, SquashRecovery, d));

        var turn = new Basis(Vector3.Up, _facing > 0 ? 0f : Mathf.Pi);
        Basis = turn * Basis.FromScale(new Vector3(_squash.X, _squash.Y, _squash.X));
    }
}
