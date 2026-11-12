using System;

namespace TheDeep;

/// <summary>Frame-rate independent scalar motion helpers. Pure logic, unit tested.</summary>
public static class Motion
{
    /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
    public static float Approach(float current, float target, float maxDelta)
    {
        if (current < target)
        {
            return MathF.Min(current + maxDelta, target);
        }

        return MathF.Max(current - maxDelta, target);
    }

    /// <summary>
    /// Exponential smoothing toward <paramref name="target"/>. <paramref name="sharpness"/> is in 1/s:
    /// higher is snappier, and the result is the same at any frame rate.
    /// </summary>
    public static float Damp(float current, float target, float sharpness, float delta)
    {
        return target + (current - target) * MathF.Exp(-sharpness * delta);
    }

    /// <summary>
    /// A critically damped spring toward <paramref name="target"/>: <paramref name="velocity"/> (units per second) is its own and
    /// carried between calls, so the value eases out of rest and into the target with no jolt, even when the target jumps.
    /// <paramref name="smoothTime"/> is roughly the seconds it lags a steadily moving target by.
    /// </summary>
    public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float delta)
    {
        float omega = 2f / MathF.Max(1e-4f, smoothTime);
        float x = omega * delta;
        float decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        float change = current - target;
        float temp = (velocity + omega * change) * delta;
        velocity = (velocity - omega * temp) * decay;
        return target + (change + temp) * decay;
    }
}
