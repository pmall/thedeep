using System;

namespace TheDeep;

/// <summary>
/// Water's numbers (from <c>player_tuning.tres</c>, the Water group), as both the pilgrim and the map's geometry check use them:
/// swimming speed, where the body floats (its feet <see cref="FloatDepth"/> under the surface), how fast it rises and sinks
/// and how quickly water changes its vertical speed, how much of a plunge's speed the surface keeps, how deep the feet must
/// be to swim, and how near the float line a jump still leaves the water.
/// </summary>
public sealed record WaterLimits(
    float SwimSpeed, float SwimAcceleration, float SwimDeceleration, float FloatDepth, float FloatStiffness, float RiseSpeed,
    float SinkSpeed, float WaterAcceleration, float SplashKeep, float EnterDepth, float JumpWindow);

/// <summary>
/// How a body moves in water, pure (the pilgrim's swim state and the map's reach planner share it). Floating, the vertical
/// speed eases toward the float line (feet <see cref="WaterLimits.FloatDepth"/> under the surface) at a speed proportional
/// to the distance, up to <see cref="WaterLimits.RiseSpeed"/> rising and <see cref="WaterLimits.SinkSpeed"/> sinking; with
/// Ballast's tanks full (sunk) it eases toward sinking at <see cref="WaterLimits.SinkSpeed"/>. Water changes the vertical
/// speed by at most <see cref="WaterLimits.WaterAcceleration"/> per second, so a plunge dips and bobs back. Horizontal
/// speed eases toward the held direction at <see cref="WaterLimits.SwimSpeed"/>.
/// </summary>
public static class WaterMotion
{
    /// <summary>The feet's height when floating on water whose surface is at <paramref name="surface"/>.</summary>
    public static float FloatLine(float surface, WaterLimits w) => surface - w.FloatDepth;

    /// <summary>The vertical speed the water steers toward, for feet at <paramref name="feetY"/>.</summary>
    public static float TargetVertical(float feetY, float surface, bool sunk, WaterLimits w) =>
        sunk ? -w.SinkSpeed : Math.Clamp((FloatLine(surface, w) - feetY) * w.FloatStiffness, -w.SinkSpeed, w.RiseSpeed);

    /// <summary>One tick of vertical speed in water.</summary>
    public static float Vertical(float vy, float feetY, float surface, bool sunk, WaterLimits w, float dt) =>
        Motion.Approach(vy, TargetVertical(feetY, surface, sunk, w), w.WaterAcceleration * dt);

    /// <summary>One tick of horizontal speed in water, holding <paramref name="dir"/> (−1, 0, +1).</summary>
    public static float Horizontal(float vx, int dir, WaterLimits w, float dt)
    {
        float target = dir * w.SwimSpeed;
        bool accelerating = target != 0f && (vx == 0f || Math.Sign(target) == Math.Sign(vx)) && Math.Abs(vx) <= w.SwimSpeed;
        return Motion.Approach(vx, target, (accelerating ? w.SwimAcceleration : w.SwimDeceleration) * dt);
    }

    /// <summary>The vertical speed just after breaking the surface: a plunge keeps only part of its speed.</summary>
    public static float Splash(float vy, WaterLimits w) => vy < 0f ? vy * w.SplashKeep : vy;

    /// <summary>Whether feet at <paramref name="feetY"/> are deep enough in water (surface at <paramref name="surface"/>) to swim.</summary>
    public static bool Deep(float feetY, float surface, WaterLimits w) => surface - feetY > w.EnterDepth;

    /// <summary>Whether a jump leaves the water from here: floating near the float line, or wading above it.</summary>
    public static bool CanJumpOut(float feetY, float surface, WaterLimits w) => feetY >= FloatLine(surface, w) - w.JumpWindow;
}
