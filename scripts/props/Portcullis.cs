using Godot;

namespace TheDeep;

/// <summary>
/// A gate of iron bars that drops across a way and rises again (a boss's arena, <see cref="BossSeal"/>). The node's
/// origin is the bottom of the gate where it closes; open, it hangs <see cref="Lift"/> higher, so keep a lintel or
/// a ceiling there to take it. Its collision is rock (never grippable) and it is not a <see cref="Block"/>: the map's
/// geometry check counts the way as open, which it is whenever no fight is on.
/// </summary>
public partial class Portcullis : Node3D
{
    /// <summary>The moving gate: its collision and its bars.</summary>
    [Export] public AnimatableBody3D Gate { get; set; } = null!;

    [Export] public CollisionShape3D Shape { get; set; } = null!;

    /// <summary>How far above its closed place the gate hangs open, meters.</summary>
    [Export] public float Lift { get; set; } = 5.5f;

    /// <summary>Seconds to fall shut (a heavy drop) and to rise open (a slow winch).</summary>
    [Export] public float FallTime { get; set; } = 0.35f;
    [Export] public float RiseTime { get; set; } = 1.8f;

    private float _open = 1f;
    private float _target = 1f;

    /// <summary>Closing or closed.</summary>
    public bool Closed => _target <= 0f;

    /// <summary>Down on the floor, barring the way.</summary>
    public bool Shut => _open <= 0f;

    /// <summary>All the way up.</summary>
    public bool Raised => _open >= 1f;

    /// <summary>Raised when the gate hits the floor (for a shake, dust).</summary>
    public event System.Action<Portcullis>? Slammed;

    public override void _Ready()
    {
        Gate.CollisionLayer = PhysicsLayers.WorldRock;
        Gate.CollisionMask = 0;
        Place();
    }

    public void Close() => _target = 0f;

    public void Open() => _target = 1f;

    public override void _PhysicsProcess(double delta)
    {
        if (_open == _target)
        {
            return;
        }

        float dt = (float)delta;
        bool falling = _target < _open;
        _open = Motion.Approach(_open, _target, dt / (falling ? FallTime : RiseTime));
        Place();
        if (falling && _open <= 0f)
        {
            Slammed?.Invoke(this);
        }
    }

    private void Place()
    {
        // Falling: accelerating like a dropped weight; rising: eased in and out like a winch.
        float t = _target < _open || _open <= 0f ? _open * _open : _open * _open * (3f - 2f * _open);
        Gate.Position = new Vector3(0f, Lift * t, 0f);
        Shape.Disabled = _open > 0.6f;
    }
}
