using System.Numerics;

namespace TheDeep;

/// <summary>An axis-aligned solid on the play plane.</summary>
public readonly record struct Box(float MinX, float MinY, float MaxX, float MaxY)
{
    /// <summary>Whether the two boxes share some area; boxes that only touch along an edge do not.</summary>
    public bool Overlaps(Box o) => MinX < o.MaxX && o.MinX < MaxX && MinY < o.MaxY && o.MinY < MaxY;

    /// <summary>Whether the point lies in the box, its edges included.</summary>
    public bool Contains(Vector2 p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

    /// <summary>Whether the other box lies wholly in this one, edges included.</summary>
    public bool Contains(Box o) => o.MinX >= MinX && o.MaxX <= MaxX && o.MinY >= MinY && o.MaxY <= MaxY;

    /// <summary>The box grown by <paramref name="margin"/> meters on every side.</summary>
    public Box Grown(float margin) => new(MinX - margin, MinY - margin, MaxX + margin, MaxY + margin);

    /// <summary>The box of the given <paramref name="size"/> (meters) centered on <paramref name="center"/>.</summary>
    public static Box Around(Vector2 center, Vector2 size) =>
        new(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f, center.X + size.X * 0.5f, center.Y + size.Y * 0.5f);
}
