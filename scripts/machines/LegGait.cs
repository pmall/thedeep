using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The stepping of a machine's legs, Godot-free. A leg's gait phase runs from 0 to 1: during the stance
/// share its foot stays planted (it slides back under the body exactly as fast as the body moves forward),
/// then it swings forward through the air. The phase advances with the distance walked, never with time,
/// so feet never skate whatever the speed.
/// </summary>
public static class LegGait
{
    /// <summary>
    /// Where the foot is relative to its rest point, in the body's frame (X forward, Y up), at
    /// <paramref name="phase"/>: <paramref name="stride"/> meters of travel while planted, lifted up to
    /// <paramref name="lift"/> meters in the swing.
    /// </summary>
    public static Vector2 Foot(float phase, float stance, float stride, float lift)
    {
        phase -= MathF.Floor(phase);
        stance = Math.Clamp(stance, 0.05f, 0.95f);
        if (phase < stance)
        {
            return new Vector2(stride * (0.5f - phase / stance), 0f);
        }

        float u = (phase - stance) / (1f - stance);
        float eased = u * u * (3f - 2f * u);
        return new Vector2(stride * (eased - 0.5f), lift * MathF.Sin(MathF.PI * u));
    }

    /// <summary>The phase change for walking <paramref name="distance"/> meters forward: a full cycle covers stride / stance meters.</summary>
    public static float Advance(float distance, float stride, float stance) =>
        distance * Math.Clamp(stance, 0.05f, 0.95f) / MathF.Max(stride, 1e-3f);
}
