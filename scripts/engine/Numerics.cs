using Godot;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace TheDeep;

/// <summary>
/// Vectors at the seam (instructions/principles.md#engine-detail): the core speaks <see cref="System.Numerics"/>, the engine its own types, and
/// every conversion between them goes through here. Play happens on the Z = 0 plane, so a point on the plane is a 2D vector
/// in the core and a 3D one at Z = 0 in the engine.
/// </summary>
public static class Numerics
{
    /// <summary>The engine's 2D vector as the core's.</summary>
    public static NVector2 ToCore(this Vector2 v) => new(v.X, v.Y);

    /// <summary>The engine's 3D vector as the core's, axes unchanged.</summary>
    public static NVector3 ToCore(this Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>A point or direction in the world, seen on the play plane (its X and Y).</summary>
    public static NVector2 Flat(this Vector3 v) => new(v.X, v.Y);

    /// <summary>The engine's 4D vector as the core's.</summary>
    public static System.Numerics.Vector4 ToCore(this Vector4 v) => new(v.X, v.Y, v.Z, v.W);

    /// <summary>The engine's color as the core's, its red, green and blue as they are written (sRGB), alpha dropped.</summary>
    public static NVector3 ToCore(this Color c) => new(c.R, c.G, c.B);

    /// <summary>The engine's quaternion as the core's.</summary>
    public static System.Numerics.Quaternion ToCore(this Quaternion q) => new(q.X, q.Y, q.Z, q.W);

    /// <summary>The core's 2D vector as the engine's.</summary>
    public static Vector2 ToGodot(this NVector2 v) => new(v.X, v.Y);

    /// <summary>The core's 3D vector as the engine's, axes unchanged.</summary>
    public static Vector3 ToGodot(this NVector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>A point or direction on the play plane, in the world (Z = 0, or <paramref name="z"/>).</summary>
    public static Vector3 OnPlane(this NVector2 v, float z = 0f) => new(v.X, v.Y, z);
}
