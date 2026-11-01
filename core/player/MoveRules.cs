using System;
using System.Numerics;

namespace TheDeep;

/// <summary>How the pilgrim's speed changes on the play plane, pure: running, falling, a blow's throw and the ledge assist's lift.</summary>
public static class MoveRules
{
    /// <summary>The horizontal speed after <paramref name="dt"/> seconds of running toward <paramref name="moveX"/> (-1, 0, +1).</summary>
    public static float Run(float vx, int moveX, bool grounded, ControlLimits l, float dt)
    {
        float target = moveX * l.RunSpeed;
        bool accelerating = target != 0f && (vx == 0f || MathF.Sign(target) == MathF.Sign(vx)) && MathF.Abs(vx) <= l.RunSpeed;
        float rate = (grounded, accelerating) switch
        {
            (true, true) => l.GroundAcceleration,
            (true, false) => l.GroundDeceleration,
            (false, true) => l.AirAcceleration,
            (false, false) => l.AirDeceleration,
        };
        return Motion.Approach(vx, target, rate * dt);
    }

    /// <summary>
    /// The vertical speed after <paramref name="dt"/> seconds of gravity: lighter near the apex while jump is held, heavier when
    /// falling, capped at the fastest fall.
    /// </summary>
    public static float Fall(float vy, bool jumpHeld, bool onFloor, ControlLimits l, float dt)
    {
        float gravity = l.Gravity;
        if (jumpHeld && MathF.Abs(vy) < l.ApexHangThreshold && !onFloor)
        {
            gravity *= l.ApexGravityMultiplier;
        }
        else if (vy < 0f)
        {
            gravity *= l.FallGravityMultiplier;
        }

        return MathF.Max(vy - gravity * dt, -l.MaxFallSpeed);
    }

    /// <summary>The velocity a blow from <paramref name="fromX"/> throws a body at <paramref name="x"/> with: away from it, and up.</summary>
    public static Vector2 Knockback(float x, float fromX, ControlLimits l) => new((x >= fromX ? 1f : -1f) * l.HurtKnockback, l.HurtLift);
}
