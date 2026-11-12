using System;
using System.Numerics;

namespace TheDeep;

/// <summary>How the pilgrim's speed changes on the play plane, pure: running, falling, a blow's throw and the ledge assist's lift.</summary>
public static class MoveRules
{
    /// <summary>The horizontal speed after <paramref name="dt"/> seconds of running toward <paramref name="moveX"/> (-1, 0, +1).</summary>
    public static float Run(float vx, int moveX, bool grounded, ControlLimits l, float dt) => grounded
        ? Toward(vx, moveX, l.RunSpeed, l.GroundAcceleration, l.GroundDeceleration, dt)
        : Air(vx, moveX, l.RunSpeed, l.AirCarrySpeed, l.AirAcceleration, l.AirDeceleration, l.AirDrag, dt);

    /// <summary>
    /// The horizontal speed after <paramref name="dt"/> seconds in the air, steering toward <paramref name="moveX"/>: faster than a run
    /// (a harpoon's release), the speed is carried, bleeding off at <paramref name="drag"/> m/s² down to a run, unless the stick pulls
    /// against it; above <paramref name="carrySpeed"/> (a dash jumped out of) it brakes at <paramref name="deceleration"/> down to
    /// that first. Otherwise as on the ground, at the air's rates. The planner flies by the same rule.
    /// </summary>
    public static float Air(float vx, int moveX, float runSpeed, float carrySpeed, float acceleration, float deceleration, float drag, float dt)
    {
        float speed = MathF.Abs(vx), sign = MathF.Sign(vx);
        if (speed <= runSpeed || moveX * sign < 0)
        {
            return Toward(vx, moveX, runSpeed, acceleration, deceleration, dt);
        }

        return speed > carrySpeed
            ? Motion.Approach(vx, sign * carrySpeed, deceleration * dt)
            : Motion.Approach(vx, sign * runSpeed, drag * dt);
    }

    /// <summary>Toward a run at <paramref name="moveX"/> (-1, 0, +1): <paramref name="acceleration"/> to speed up, <paramref name="deceleration"/> to brake or turn.</summary>
    private static float Toward(float vx, int moveX, float runSpeed, float acceleration, float deceleration, float dt)
    {
        float target = moveX * runSpeed;
        bool accelerating = target != 0f && (vx == 0f || MathF.Sign(target) == MathF.Sign(vx)) && MathF.Abs(vx) <= runSpeed;
        return Motion.Approach(vx, target, (accelerating ? acceleration : deceleration) * dt);
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
