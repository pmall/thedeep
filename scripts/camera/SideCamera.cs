using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Locked side-view camera looking down -Z at the play plane. Follows the player by the core's <see cref="CameraFollow"/> (eased,
/// looking ahead, steady through jumps, leading falls), never showing outside the room's bounds; it shakes on hard landings and
/// on whatever calls <see cref="AddShake"/>, and a blow that lands kicks it (<see cref="Kick"/>).
/// Narrow-FOV perspective (not orthographic) so the backdrop and foreground layers get parallax.
/// </summary>
public partial class SideCamera : Camera3D
{
    [Export] public Player Target { get; set; } = null!;
    [Export] public World World { get; set; } = null!;

    /// <summary>The gameplay camera's distance from the play plane, unless a scene sets <see cref="Distance"/>.</summary>
    public const float DefaultDistance = 22f;

    /// <summary>Distance from the play plane (Z = 0) to the camera.</summary>
    [Export] public float Distance { get; set; } = DefaultDistance;

    /// <summary>Frames the player slightly below the center of the screen: the focus sits this far above its feet when upright.</summary>
    [Export] public float VerticalOffset { get; set; } = 1.5f;

    /// <summary>The body's center above its feet: the camera follows the center, so a pilgrim on a wall or ceiling stays framed.</summary>
    [Export] public float BodyCenter { get; set; } = 0.8f;

    /// <summary>The follow's eases, seconds: about how long the camera lags a body moving steadily (<see cref="CameraFollow"/>).</summary>
    [Export] public float HorizontalTime { get; set; } = 0.2f;
    [Export] public float VerticalTime { get; set; } = 0.24f;

    /// <summary>How far ahead of the player (meters) the camera looks when moving, how fast it swings over (seconds), and the
    /// horizontal speed (m/s) above which the look-ahead switches to the direction of travel.</summary>
    [Export] public float LookAhead { get; set; } = 2.5f;
    [Export] public float LookAheadTime { get; set; } = 0.45f;
    [Export] public float LookAheadMinSpeed { get; set; } = 1f;

    /// <summary>How high (meters) a jump rises over the floor it left before the camera follows it up.</summary>
    [Export] public float RiseBand { get; set; } = 2.2f;

    /// <summary>How far below a fast fall the camera looks (meters), from the fall speed where it starts to the one where it is
    /// full (m/s).</summary>
    [Export] public float FallLead { get; set; } = 4.5f;
    [Export] public float FallLeadFrom { get; set; } = 8f;
    [Export] public float FallLeadFull { get; set; } = 18f;

    /// <summary>The farthest the camera trails where it wants to be when the body runs from it, across and up or down
    /// (meters): a dash or a long fall stays on screen.</summary>
    [Export] public float LeashX { get; set; } = 4.5f;
    [Export] public float LeashY { get; set; } = 2.5f;

    /// <summary>How long a blow's kick (<see cref="Kick"/>) takes to ease back, seconds.</summary>
    [Export] public float KickTime { get; set; } = 0.07f;

    /// <summary>Landing speed (m/s) above which a landing shakes the camera, and the speed of the strongest shake.</summary>
    [Export] public float ShakeLandingSpeed { get; set; } = 13f;
    [Export] public float ShakeLandingMax { get; set; } = 22f;

    /// <summary>Largest shake offset, meters; shake fades at <see cref="ShakeDecay"/> per second.</summary>
    [Export] public float ShakeAmplitude { get; set; } = 0.12f;
    [Export] public float ShakeDecay { get; set; } = 2.5f;

    private CameraFollow? _follow;
    private CameraFollow Follow => _follow ??= new CameraFollow(new FollowLimits(LookAhead, LookAheadMinSpeed, LookAheadTime,
        HorizontalTime, VerticalTime, RiseBand, FallLead, FallLeadFrom, FallLeadFull, LeashX, LeashY, KickTime));

    private bool _framingShot;
    private float _followFov;
    private float _trauma;
    private float _shakeTime;

    public override void _Ready()
    {
        // Moved in _Process from the target's interpolated transform, so it must not be interpolated itself.
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        Target.Respawned += SnapToTarget;
        Target.Arrived += SnapToTarget;
        Target.Landed += OnLanded;
        SnapToTarget();
    }

    public override void _ExitTree()
    {
        Target.Respawned -= SnapToTarget;
        Target.Arrived -= SnapToTarget;
        Target.Landed -= OnLanded;
    }

    /// <summary>Adds shake (0 to 1); the offset grows with its square, so small amounts stay subtle.</summary>
    public void AddShake(float amount) => _trauma = Mathf.Clamp(_trauma + amount, 0f, 1f);

    /// <summary>A blow lands: the view is thrown <paramref name="offset"/> meters along it and eases back (the strongest under way wins).</summary>
    public void Kick(NVector2 offset) => Follow.Kick(offset);

    public override void _Process(double delta)
    {
        // Every frame, framing shots too: the room may have changed, and a wide shot stands farther back.
        World.Room.FollowCamera(GlobalPosition.Z);
        if (_framingShot)
        {
            return;
        }

        float dt = (float)delta;
        _trauma = Mathf.Max(0f, _trauma - ShakeDecay * dt);
        _shakeTime += dt;
        Vector3 body = Target.GetGlobalTransformInterpolated() * new Vector3(0f, BodyCenter, 0f) - new Vector3(0f, BodyCenter, 0f);
        CameraFooting footing = CameraFollow.FootingOf(Target.CurrentState.Motion, Target.IsOnFloor(), Target.IsUpright);
        Follow.Step(dt, Framed(body), Target.Velocity.Flat(), footing, View());
        ApplyFocus();
    }

    /// <summary>Jumps straight to the target with no smoothing (on start and respawn).</summary>
    public void SnapToTarget()
    {
        if (_framingShot)
        {
            return;
        }

        Vector3 body = Target.GlobalTransform * new Vector3(0f, BodyCenter, 0f) - new Vector3(0f, BodyCenter, 0f);
        Follow.Snap(Framed(body), Target.Facing, View());
        ApplyFocus();
    }

    /// <summary>
    /// Stops following and holds a fixed shot from <paramref name="position"/> (looking down -Z),
    /// ignoring the room bounds.
    /// <paramref name="fov"/> of 0 keeps the follow camera's field of view.
    /// </summary>
    public void FrameShot(Vector3 position, float fov)
    {
        if (!_framingShot)
        {
            _framingShot = true;
            _followFov = Fov;
        }

        GlobalPosition = position;
        Fov = fov > 0f ? fov : _followFov;
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
        SnapToTarget();
    }

    private void ApplyFocus()
    {
        // Smooth, mostly vertical shake from a few incommensurate sines: a jolt, never a rattle.
        float shake = _trauma * _trauma * ShakeAmplitude;
        var offset = new Vector2(
            0.5f * Mathf.Sin(_shakeTime * 37f) * Mathf.Sin(_shakeTime * 13f),
            Mathf.Sin(_shakeTime * 43f + 1.3f) * Mathf.Cos(_shakeTime * 17f)) * shake;
        NVector2 focus = Follow.Focus + Follow.KickOffset;
        GlobalPosition = new Vector3(focus.X + offset.X, focus.Y + offset.Y, Distance);
    }

    private void OnLanded(float impactSpeed)
    {
        if (impactSpeed > ShakeLandingSpeed)
        {
            AddShake(0.75f * Mathf.Clamp((impactSpeed - ShakeLandingSpeed) / (ShakeLandingMax - ShakeLandingSpeed), 0.25f, 1f));
        }
    }

    /// <summary>The point the camera centers on for a body whose center is at <paramref name="body"/>: a little above it.</summary>
    private NVector2 Framed(Vector3 body) => new(body.X, body.Y + VerticalOffset);

    /// <summary>The room's bounds and the view's half size on the play plane.</summary>
    private ViewBounds View()
    {
        float halfHeight = CameraMath.HalfHeightAtDistance(Distance, Fov);
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float halfWidth = halfHeight * (viewport.Y > 0f ? viewport.X / viewport.Y : 16f / 9f);
        return new ViewBounds(World.Room.BoundsMin.ToCore(), World.Room.BoundsMax.ToCore(), new NVector2(halfWidth, halfHeight));
    }
}
