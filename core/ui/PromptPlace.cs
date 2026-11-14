using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Pure: where an action prompt's plate stands on the screen. Its place in the world shows at a point on the screen; the plate is
/// centered on that point, or reaches east of it (its west edge on the point) or west of it (its east edge on the point), so a prompt
/// beside something bright stays clear of it; and it is kept whole on the canvas, a margin inside its edges. Screen pixels, y down.
/// </summary>
public static class PromptPlace
{
    /// <summary>
    /// The top left corner of a plate <paramref name="size"/> big for a place showing at <paramref name="at"/>, reaching
    /// <paramref name="reach"/> (0 centered, +1 east, −1 west), on a canvas <paramref name="canvas"/> big, kept
    /// <paramref name="margin"/> inside its edges (a plate too big for that is centered on the canvas instead).
    /// </summary>
    public static Vector2 TopLeft(Vector2 at, Vector2 size, int reach, Vector2 canvas, float margin)
    {
        float x = at.X - size.X * 0.5f * (1 - Math.Sign(reach));
        float y = at.Y - size.Y * 0.5f;
        return new Vector2(Within(x, size.X, canvas.X, margin), Within(y, size.Y, canvas.Y, margin));
    }

    private static float Within(float start, float length, float span, float margin) =>
        length + 2f * margin > span ? (span - length) * 0.5f : Math.Clamp(start, margin, span - margin - length);
}
