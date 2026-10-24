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
    private bool _framingShot;
    private float _followFov;
    private CameraAttributes? _followAttributes;

    /// <summary>
    /// The follow attributes refocused for the current shot. One copy, reused: a fresh duplicate per shot
    /// leaves resources only C# wrappers hold, and finalizing those at quit crashes Godot .NET 4.7.2.
    /// </summary>
    private CameraAttributesPractical? _shotAttributes;
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
        if (_framingShot)
        {
            return;
        }

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
        if (_framingShot)
        {
            return;
        }

        Vector3 target = Target.GlobalPosition;
        _lookAheadTarget = Target.Facing * LookAhead;
        _lookAhead = _lookAheadTarget;
        _focus = ClampToRoom(new Vector2(target.X + _lookAhead, target.Y + VerticalOffset));
        ApplyFocus();
    }

    /// <summary>
    /// Stops following and holds a fixed shot from <paramref name="position"/> (looking down -Z),
    /// ignoring the room bounds. Depth of field is refocused on the play plane at that distance.
    /// <paramref name="fov"/> of 0 keeps the follow camera's field of view.
    /// </summary>
    public void FrameShot(Vector3 position, float fov)
    {
        if (!_framingShot)
        {
            _framingShot = true;
            _followFov = Fov;
            _followAttributes = Attributes;
        }

        GlobalPosition = position;
        Fov = fov > 0f ? fov : _followFov;
        if (_followAttributes is CameraAttributesPractical practical)
        {
            _shotAttributes ??= (CameraAttributesPractical)practical.Duplicate();
            RefocusOnPlayPlane(_shotAttributes, practical, position.Z - Distance);
            Attributes = _shotAttributes;
        }
    }

    /// <summary>Goes back to following the target after <see cref="FrameShot"/>.</summary>
    public void ResumeFollow()
    {
        if (!_framingShot)
        {
            return;
        }

        _framingShot = false;
        Fov = _followFov;
        Attributes = _followAttributes;
        SnapToTarget();
    }

    /// <summary>Sets <paramref name="attributes"/> to the blur bands of <paramref name="source"/> moved by <paramref name="shift"/> meters, so the play plane stays sharp.</summary>
    private static void RefocusOnPlayPlane(CameraAttributesPractical attributes, CameraAttributesPractical source, float shift)
    {
        attributes.DofBlurFarDistance = Mathf.Max(source.DofBlurFarDistance + shift, 1f);
        attributes.DofBlurNearDistance = Mathf.Max(source.DofBlurNearDistance + shift, 0.1f);
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
