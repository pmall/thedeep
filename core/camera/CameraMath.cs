using System;

namespace TheDeep;

/// <summary>Camera bounds math. Pure logic, unit tested.</summary>
public static class CameraMath
{
    /// <summary>
    /// Clamps a view center on one axis so a view of half-size <paramref name="halfExtent"/> stays inside
    /// [<paramref name="min"/>, <paramref name="max"/>]. If the room is smaller than the view, centers it.
    /// </summary>
    public static float ClampAxis(float center, float halfExtent, float min, float max)
    {
        if (max - min <= 2f * halfExtent)
        {
            return (min + max) * 0.5f;
        }

        return Math.Clamp(center, min + halfExtent, max - halfExtent);
    }

    /// <summary>Half the visible height at <paramref name="distance"/> for a vertical field of view in degrees.</summary>
    public static float HalfHeightAtDistance(float distance, float verticalFovDegrees)
    {
        return distance * MathF.Tan(verticalFovDegrees * MathF.PI / 360f);
    }

    /// <summary>
    /// Whether a point on the play plane is on screen: within a view of half-size (<paramref name="halfWidth"/>, <paramref name="halfHeight"/>)
    /// centered on (<paramref name="centerX"/>, <paramref name="centerY"/>), its edges drawn in by <paramref name="margin"/> meters.
    /// </summary>
    public static bool Frames(float centerX, float centerY, float halfWidth, float halfHeight, float pointX, float pointY, float margin)
    {
        return MathF.Abs(pointX - centerX) <= halfWidth - margin && MathF.Abs(pointY - centerY) <= halfHeight - margin;
    }
}
