using System;

namespace TheDeep;

/// <summary>
/// The ledge assist's shape (owner playtest 2026-11-01), shared by the pilgrim and the map's planner so they lift the same way: an
/// airborne body pressing into a wall whose top is a little above its feet is set onto the top, just past the edge, keeping its way.
/// </summary>
public static class LedgeAssist
{
    /// <summary>How far past the wall's face the body is set down, meters.</summary>
    public const float Reach = 0.15f;

    /// <summary>The least speed it leaves the body with, as a share of the run speed.</summary>
    public const float SpeedShare = 0.6f;

    /// <summary>The speed along the top after the lift: what the body pushed with, at least <see cref="SpeedShare"/> of a run.</summary>
    public static float Speed(float pushSpeed, float runSpeed) => MathF.Max(MathF.Abs(pushSpeed), SpeedShare * runSpeed);
}
