using System.Numerics;

namespace TheDeep;

/// <summary>
/// The room as the camera may frame it: the room's bounds (<paramref name="Min"/>, <paramref name="Max"/>) and half the view's size
/// on the play plane (<paramref name="Half"/>), meters. A view center is kept so the view stays inside the bounds.
/// </summary>
public readonly record struct ViewBounds(Vector2 Min, Vector2 Max, Vector2 Half)
{
    /// <summary>The nearest view center to <paramref name="center"/> whose view stays inside the bounds (centered if it can't).</summary>
    public Vector2 Clamp(Vector2 center) => new(
        CameraMath.ClampAxis(center.X, Half.X, Min.X, Max.X), CameraMath.ClampAxis(center.Y, Half.Y, Min.Y, Max.Y));
}
