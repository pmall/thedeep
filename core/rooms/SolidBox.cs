using System.Numerics;

namespace TheDeep;

/// <summary>A room's solid in space, axis-aligned: a gameplay block with its depth (what light falls on).</summary>
public readonly record struct SolidBox(Vector3 Min, Vector3 Max)
{
    /// <summary>Whether <paramref name="p"/> is strictly inside the solid: a point on one of its faces is outside.</summary>
    public bool Contains(Vector3 p) => p.X > Min.X && p.X < Max.X && p.Y > Min.Y && p.Y < Max.Y && p.Z > Min.Z && p.Z < Max.Z;

    /// <summary>The solid of the given <paramref name="size"/> (meters) centered on <paramref name="center"/>.</summary>
    public static SolidBox Around(Vector3 center, Vector3 size) => new(center - size * 0.5f, center + size * 0.5f);
}
