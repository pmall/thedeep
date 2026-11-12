using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Where the side camera looks, pure: a point on the play plane that follows a target
/// (the body, framed a little under the screen's middle) on critically damped springs, so it eases out of rest and into place and
/// never jolts when the target jumps (a landing, a turn). Ahead of the way the body runs it looks a little farther. Vertically it
/// reads the body's footing (<see cref="CameraFooting"/>): standing, it settles on the floor's height; in a jump it keeps the
/// height the body left until the body rises out of <see cref="FollowLimits.RiseBand"/> or drops below it, so a hop doesn't rock
/// the screen; a fast fall looks down ahead of the body, so the landing is seen. A leash keeps a body that runs from the camera on screen
/// whatever its speed, and the view stays inside the room (<see cref="CameraMath.ClampAxis"/>). A blow that lands kicks the view
/// along it (<see cref="Kick"/>), eased back on its own spring.
/// </summary>
public sealed class CameraFollow
{
    private readonly FollowLimits _l;
    private Vector2 _velocity;
    private float _lookAhead;
    private float _lookAheadVelocity;
    private int _lookAheadSide = 1;
    private float _floorY;
    private Vector2 _kickVelocity;

    /// <summary>A follow by the numbers of <paramref name="limits"/>, looking at the origin until <see cref="Snap"/>.</summary>
    public CameraFollow(FollowLimits limits) => _l = limits;

    /// <summary>Where the camera looks now, on the play plane (its shake and kick aside).</summary>
    public Vector2 Focus { get; private set; }

    /// <summary>How far a blow's kick throws the view off <see cref="Focus"/> now, meters; back to zero within the kick's ease.</summary>
    public Vector2 KickOffset { get; private set; }

    /// <summary>A blow lands: the view is thrown by <paramref name="offset"/> meters (the strongest kick under way wins), then eases
    /// back.</summary>
    public void Kick(Vector2 offset)
    {
        if (offset.LengthSquared() > KickOffset.LengthSquared())
        {
            KickOffset = offset;
            _kickVelocity = Vector2.Zero;
        }
    }

    /// <summary>
    /// Straight onto <paramref name="target"/> with no ease (a start, a respawn, an arrival), looking ahead toward
    /// <paramref name="facing"/> (+1 right, -1 left), within <paramref name="view"/> (<see cref="Step"/>).
    /// </summary>
    public void Snap(Vector2 target, int facing, ViewBounds view)
    {
        _lookAheadSide = facing < 0 ? -1 : 1;
        _lookAhead = _lookAheadSide * _l.LookAhead;
        _lookAheadVelocity = 0f;
        _velocity = Vector2.Zero;
        KickOffset = Vector2.Zero;
        _kickVelocity = Vector2.Zero;
        _floorY = target.Y;
        Focus = view.Clamp(new Vector2(target.X + _lookAhead, target.Y));
    }

    /// <summary>
    /// One frame of <paramref name="dt"/> seconds following <paramref name="target"/>, moving at <paramref name="velocity"/> m/s on
    /// the given <paramref name="footing"/>, the view kept within <paramref name="view"/>. Returns the new <see cref="Focus"/>.
    /// </summary>
    public Vector2 Step(float dt, Vector2 target, Vector2 velocity, CameraFooting footing, ViewBounds view)
    {
        if (MathF.Abs(velocity.X) > _l.LookAheadMinSpeed)
        {
            _lookAheadSide = velocity.X > 0f ? 1 : -1;
        }

        _lookAhead = Motion.SmoothDamp(_lookAhead, _lookAheadSide * _l.LookAhead, ref _lookAheadVelocity, _l.LookAheadTime, dt);
        Vector2 desired = view.Clamp(new Vector2(target.X + _lookAhead, FollowY(target.Y, velocity.Y, footing)));

        float vx = _velocity.X, vy = _velocity.Y;
        var focus = new Vector2(
            Motion.SmoothDamp(Focus.X, desired.X, ref vx, _l.HorizontalTime, dt),
            Motion.SmoothDamp(Focus.Y, desired.Y, ref vy, _l.VerticalTime, dt));

        // The leash: past it, the camera is dragged along at the body's own pace, and eases out of it when the body slows.
        focus = new Vector2(Leash(focus.X, desired.X, _l.LeashX, velocity.X, ref vx), Leash(focus.Y, desired.Y, _l.LeashY, velocity.Y, ref vy));
        float kx = _kickVelocity.X, ky = _kickVelocity.Y;
        KickOffset = new Vector2(
            Motion.SmoothDamp(KickOffset.X, 0f, ref kx, _l.KickTime, dt), Motion.SmoothDamp(KickOffset.Y, 0f, ref ky, _l.KickTime, dt));
        _kickVelocity = new Vector2(kx, ky);

        Vector2 clamped = view.Clamp(focus);
        _velocity = new Vector2(clamped.X != focus.X ? 0f : vx, clamped.Y != focus.Y ? 0f : vy);
        Focus = clamped;
        return Focus;
    }

    /// <summary>The footing of a body in <paramref name="motion"/>, on a floor or not, upright or not (on a Mag-grip surface).</summary>
    public static CameraFooting FootingOf(MotionMode motion, bool onFloor, bool upright) => motion switch
    {
        MotionMode.Grip or MotionMode.Swim or MotionMode.Sink or MotionMode.Rise or MotionMode.Zip => CameraFooting.Tracked,
        _ when !upright => CameraFooting.Tracked,
        _ => onFloor ? CameraFooting.Ground : CameraFooting.Air,
    };

    /// <summary>The height the camera wants for a target at <paramref name="y"/> on <paramref name="footing"/>.</summary>
    private float FollowY(float y, float vy, CameraFooting footing)
    {
        switch (footing)
        {
            case CameraFooting.Ground:
            case CameraFooting.Tracked:
                _floorY = y;
                break;
            case CameraFooting.Air:
                _floorY = Math.Clamp(_floorY, y - _l.RiseBand, y);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(footing), footing, null);
        }

        float fall = -vy;
        float lead = footing == CameraFooting.Air && fall > _l.FallLeadFrom
            ? _l.FallLead * MathF.Min(1f, (fall - _l.FallLeadFrom) / MathF.Max(1e-3f, _l.FallLeadFull - _l.FallLeadFrom))
            : 0f;
        return _floorY - lead;
    }

    private static float Leash(float value, float desired, float leash, float bodyVelocity, ref float velocity)
    {
        // Only a body running away from the camera pulls it: a target that jumps (a landing) is eased to.
        float off = value - desired;
        if (MathF.Abs(off) <= leash || off * bodyVelocity >= 0f)
        {
            return value;
        }

        velocity = bodyVelocity;
        return desired + MathF.Sign(off) * leash;
    }
}

/// <summary>
/// The room as the camera may frame it: the room's bounds (<paramref name="Min"/>, <paramref name="Max"/>) and half the view's size
/// on the play plane (<paramref name="Half"/>), meters. A view center is kept so the view stays inside the bounds.
/// </summary>
public readonly record struct ViewBounds(Vector2 Min, Vector2 Max, Vector2 Half)
{
    /// <summary>The nearest view center to <paramref name="center"/> whose view stays inside the bounds (centered if it can't).</summary>
    public Vector2 Clamp(Vector2 center) => new(
        CameraMath.ClampAxis(center.X, Half.X, Min.X, Max.X), CameraMath.ClampAxis(center.Y, Half.Y, Min.Y, Max.Y));
}
