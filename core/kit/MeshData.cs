using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Vertex data of a generated kit mesh, Godot-free (built by <see cref="MeshBuilder"/>, handed to Godot by
/// KitMesh). Every vertex carries the surface frame the materials need instead of texture UVs:
/// its position on a rectangular <em>patch</em> (a box face, one side of a swept profile) in meters,
/// the patch size, and the directions in which that position grows. Materials draw panels, seams and
/// edge wear from it (assets/shaders/lib/surface.gdshaderinc).
/// </summary>
public sealed class MeshData
{
    public List<Vector3> Positions { get; } = new();
    public List<Vector3> Normals { get; } = new();

    /// <summary>XYZ: direction of +U. W: sign such that cross(normal, xyz) * w is the direction of +V (Godot's convention).</summary>
    public List<Vector4> Tangents { get; } = new();

    /// <summary>Position on the patch from its corner, meters.</summary>
    public List<Vector2> Uvs { get; } = new();

    /// <summary>Size of the patch, meters. Negative along an axis that wraps around (a pipe's girth): no edge there.</summary>
    public List<Vector2> PatchSizes { get; } = new();

    /// <summary>Height above the mesh's lowest point, meters (grime by height).</summary>
    public List<float> Heights { get; } = new();

    /// <summary>Random value in [0, 1) per part, so the parts of one piece don't look identical.</summary>
    public List<float> Variations { get; } = new();

    /// <summary>Triangles, clockwise seen from the front (Godot's front-face convention).</summary>
    public List<int> Indices { get; } = new();

    public int VertexCount => Positions.Count;
    public int TriangleCount => Indices.Count / 3;

    public (Vector3 Min, Vector3 Max) Bounds()
    {
        if (Positions.Count == 0)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        Vector3 min = Positions[0], max = Positions[0];
        foreach (Vector3 p in Positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return (min, max);
    }
}
