using System.Numerics;

namespace TheDeep;

/// <summary>A room's solid in space, axis-aligned: a gameplay block with its depth (what light falls on).</summary>
public readonly record struct SolidBox(Vector3 Min, Vector3 Max)
{
    public bool Contains(Vector3 p) => p.X > Min.X && p.X < Max.X && p.Y > Min.Y && p.Y < Max.Y && p.Z > Min.Z && p.Z < Max.Z;

    public static SolidBox Around(Vector3 center, Vector3 size) => new(center - size * 0.5f, center + size * 0.5f);
}
