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
}
