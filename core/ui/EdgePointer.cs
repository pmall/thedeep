using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Where to point at something off screen: a mark held inside the screen's edge, on the line from the screen's
/// center toward the thing, turned to face it. Used to show the Harpoon's target when the camera doesn't frame it.
/// Pure logic, unit tested.
/// </summary>
public static class EdgePointer
{
    /// <summary>
    /// The mark for a target at <paramref name="target"/> (screen pixels; may lie outside the screen) on a screen of
    /// <paramref name="size"/>, kept <paramref name="margin"/> pixels inside its edge. Null when the target is
    /// already on screen, at least <paramref name="margin"/> inside the edge: then nothing needs pointing at.
    /// </summary>
    public static Mark? For(Vector2 target, Vector2 size, float margin)
    {
        Vector2 min = new(margin), max = size - new Vector2(margin);
        if (target.X >= min.X && target.X <= max.X && target.Y >= min.Y && target.Y <= max.Y)
        {
            return null;
        }

        Vector2 center = size * 0.5f;
        Vector2 toward = target - center;
        Vector2 half = center - new Vector2(margin);
        float sx = toward.X == 0f ? float.MaxValue : half.X / MathF.Abs(toward.X);
        float sy = toward.Y == 0f ? float.MaxValue : half.Y / MathF.Abs(toward.Y);
        float scale = MathF.Min(sx, sy);
        return new Mark(center + toward * scale, Vector2.Normalize(toward));
    }

    /// <summary>A mark at <paramref name="Position"/> (screen pixels) pointing along <paramref name="Direction"/> (unit, screen axes: y down).</summary>
    public readonly record struct Mark(Vector2 Position, Vector2 Direction);
}
