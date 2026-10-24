using Godot;

namespace TheDeep;

/// <summary>
/// Locked side-view camera looking down -Z at the play plane. Smoothly follows the player with a
/// look-ahead in the direction of travel, and never shows outside the room's bounds.
/// Narrow-FOV perspective (not orthographic) so the backdrop and foreground layers get parallax.
/// </summary>
public partial class SideCamera : Camera3D
{
    [Export] public Player Target { get; set; } = null!;
    [Export] public Room Room { get; set; } = null!;

    /// <summary>Distance from the play plane (Z = 0) to the camera.</summary>
    [Export] public float Distance { get; set; } = 22f;

    /// <summary>Frames the player slightly below the center of the screen.</summary>
    [Export] public float VerticalOffset { get; set; } = 1.5f;

    /// <summary>Follow speeds in 1/s; higher is tighter.</summary>
    [Export] public float HorizontalSharpness { get; set; } = 5f;
    [Export] public float VerticalSharpness { get; set; } = 3.5f;

    /// <summary>How far ahead of the player (meters) the camera looks when moving.</summary>
    [Export] public float LookAhead { get; set; } = 2.5f;
    [Export] public float LookAheadSharpness { get; set; } = 2f;

    /// <summary>Horizontal speed above which the look-ahead switches to the direction of travel.</summary>
    [Export] public float LookAheadMinSpeed { get; set; } = 1f;

    private Vector2 _focus;
    private float _lookAhead;
    private float _lookAheadTarget;

    public override void _Ready()
    {
        // Moved in _Process from the target's interpolated transform, so it must not be interpolated itself.
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        Target.Respawned += SnapToTarget;
        SnapToTarget();
    }

    public override void _ExitTree()
    {
        Target.Respawned -= SnapToTarget;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vector3 target = Target.GetGlobalTransformInterpolated().Origin;

        float vx = Target.Velocity.X;
        if (Mathf.Abs(vx) > LookAheadMinSpeed)
        {
            _lookAheadTarget = Mathf.Sign(vx) * LookAhead;
        }

        _lookAhead = Motion.Damp(_lookAhead, _lookAheadTarget, LookAheadSharpness, dt);

        Vector2 desired = ClampToRoom(new Vector2(target.X + _lookAhead, target.Y + VerticalOffset));
        _focus = ClampToRoom(new Vector2(
            Motion.Damp(_focus.X, desired.X, HorizontalSharpness, dt),
            Motion.Damp(_focus.Y, desired.Y, VerticalSharpness, dt)));
        ApplyFocus();
    }

    /// <summary>Jumps straight to the target with no smoothing (on start and respawn).</summary>
    public void SnapToTarget()
    {
        Vector3 target = Target.GlobalPosition;
        _lookAheadTarget = Target.Facing * LookAhead;
        _lookAhead = _lookAheadTarget;
        _focus = ClampToRoom(new Vector2(target.X + _lookAhead, target.Y + VerticalOffset));
        ApplyFocus();
    }

    private void ApplyFocus()
    {
        GlobalPosition = new Vector3(_focus.X, _focus.Y, Distance);
    }

    private Vector2 ClampToRoom(Vector2 center)
    {
        float halfHeight = CameraMath.HalfHeightAtDistance(Distance, Fov);
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float halfWidth = halfHeight * (viewport.Y > 0f ? viewport.X / viewport.Y : 16f / 9f);
        return new Vector2(
            CameraMath.ClampAxis(center.X, halfWidth, Room.BoundsMin.X, Room.BoundsMax.X),
            CameraMath.ClampAxis(center.Y, halfHeight, Room.BoundsMin.Y, Room.BoundsMax.Y));
    }
}
