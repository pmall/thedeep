using System;
using System.Numerics;

namespace TheDeep;

/// <summary>One tick of a harpoon pull (<see cref="HarpoonPull.Step"/>): the new speed, the velocity to move at, and whether this tick reaches the anchor.</summary>
public readonly record struct PullStep(float Speed, Vector2 Velocity, bool Arriving);

/// <summary>
/// The pull of a bitten Harpoon line, pure (docs/gameplay.md#harpoon): the pilgrim gathers speed toward the anchor, a last step
/// lands exactly on it, and a pull that a solid keeps blocked for a few ticks gives up.
/// </summary>
public static class HarpoonPull
{
    /// <summary>The pull is done once the center is this near the anchor, meters.</summary>
    public const float ArrivedWithin = 0.05f;

    /// <summary>A pull moving less than this share of its speed is blocked for the tick.</summary>
    public const float StuckShare = 0.25f;

    /// <summary>Ticks in a row a pull must be blocked (<see cref="StuckShare"/>) before it lets go: one slow tick is a bump.</summary>
    public const int StuckTicks = 4;

    /// <summary>The unit direction of <paramref name="to"/> (the way to the anchor); straight up when the two coincide.</summary>
    public static Vector2 Direction(Vector2 to)
    {
        float distance = to.Length();
        return distance > 1e-4f ? to / distance : new Vector2(0f, 1f);
    }

    /// <summary>The speed a bite starts the pull at: the part of the body's <paramref name="velocity"/> already along <paramref name="direction"/>.</summary>
    public static float BiteSpeed(Vector2 velocity, Vector2 direction) => MathF.Max(0f, Vector2.Dot(velocity, direction));

    /// <summary>
    /// One tick of the pull toward an anchor at <paramref name="to"/> (from the center), at <paramref name="speed"/> m/s: the speed
    /// eases toward <paramref name="zipSpeed"/> by <paramref name="acceleration"/> (m/s²) over <paramref name="dt"/> seconds. When the
    /// anchor is within a tick's travel the velocity is the one that lands on it.
    /// </summary>
    public static PullStep Step(Vector2 to, float speed, float zipSpeed, float acceleration, float dt)
    {
        float next = Motion.Approach(speed, zipSpeed, acceleration * dt);
        bool arriving = to.Length() <= next * dt;
        return new PullStep(next, arriving ? to / dt : Direction(to) * next, arriving);
    }

    /// <summary>
    /// The count of ticks in a row the pull has been blocked, after a tick that moved <paramref name="moved"/> meters where
    /// <paramref name="expected"/> was due, <paramref name="blockedTicks"/> being the count before: 0 when it moved freely.
    /// </summary>
    public static int Blocked(float moved, float expected, int blockedTicks) => moved < expected * StuckShare ? blockedTicks + 1 : 0;

    /// <summary>Whether <paramref name="blockedTicks"/> in a row is enough to let go.</summary>
    public static bool GivesUp(int blockedTicks) => blockedTicks >= StuckTicks;
}
