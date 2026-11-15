using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Builds kit meshes from a few primitives (bevelled boxes, sweeps, caps), Godot-free. Every vertex gets
/// the surface frame of <see cref="MeshData"/>. Triangles are oriented from the vertex normals, so
/// primitives only need outward normals, not a careful winding.
/// </summary>
public sealed partial class MeshBuilder
{
    private const float HeightUnset = float.NaN;

    private readonly MeshData _mesh = new();

    /// <summary>Placement applied to everything added next: rotation and translation only (UVs are meters).</summary>
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    /// <summary>Per-part random value given to the vertices added next (see <see cref="MeshData.Variations"/>).</summary>
    public float Variation { get; set; }

    /// <summary>How deep in a hollow the vertices added next lie (see <see cref="MeshData.Cavities"/>); 0 unless set.</summary>
    public float Cavity { get; set; }

    /// <summary>Empties the builder to build again, keeping its buffers (a mesh rebuilt every frame allocates nothing).</summary>
    public void Clear()
    {
        _mesh.Positions.Clear();
        _mesh.Normals.Clear();
        _mesh.Tangents.Clear();
        _mesh.Uvs.Clear();
        _mesh.PatchSizes.Clear();
        _mesh.Heights.Clear();
        _mesh.Variations.Clear();
        _mesh.Cavities.Clear();
        _mesh.Indices.Clear();
        Transform = Matrix4x4.Identity;
        Variation = 0f;
        Cavity = 0f;
    }

    /// <summary>
    /// Adds a vertex. <paramref name="uAxis"/> and <paramref name="vAxis"/> are the directions in which
    /// <paramref name="uv"/> grows; they are fitted to the normal.
    /// </summary>
    public int Vertex(Vector3 position, Vector3 normal, Vector3 uAxis, Vector3 vAxis, Vector2 uv, Vector2 patchSize)
    {
        Vector3 n = Vector3.Normalize(Vector3.TransformNormal(normal, Transform));
        Vector3 u = Vector3.TransformNormal(uAxis, Transform);
        Vector3 v = Vector3.TransformNormal(vAxis, Transform);
        Vector3 t = u - n * Vector3.Dot(n, u);
        if (t.LengthSquared() < 1e-8f)
        {
            // U runs along the normal (a corner vertex): pick the tangent that makes +V come out right.
            t = Vector3.Cross(v, n);
        }

        t = Vector3.Normalize(t);
        float w = Vector3.Dot(Vector3.Cross(n, t), v) >= 0f ? 1f : -1f;

        _mesh.Positions.Add(Vector3.Transform(position, Transform));
        _mesh.Normals.Add(n);
        _mesh.Tangents.Add(new Vector4(t, w));
        _mesh.Uvs.Add(uv);
        _mesh.PatchSizes.Add(patchSize);
        _mesh.Heights.Add(HeightUnset);
        _mesh.Variations.Add(Variation);
        _mesh.Cavities.Add(Cavity);
        return _mesh.Positions.Count - 1;
    }

    /// <summary>Adds a triangle, facing the side its vertex normals point to. Degenerate triangles are dropped.</summary>
    public void Triangle(int a, int b, int c)
    {
        List<Vector3> p = _mesh.Positions;
        Vector3 geometric = Vector3.Cross(p[b] - p[a], p[c] - p[a]);
        if (geometric.LengthSquared() < 1e-14f)
        {
            return;
        }

        Vector3 normal = _mesh.Normals[a] + _mesh.Normals[b] + _mesh.Normals[c];
        if (Vector3.Dot(geometric, normal) < 0f)
        {
            (b, c) = (c, b);
        }

        // a, b, c now run counterclockwise seen from the front; Godot's front faces are clockwise.
        _mesh.Indices.Add(a);
        _mesh.Indices.Add(c);
        _mesh.Indices.Add(b);
    }

    /// <summary>Adds a quad given as a loop (either direction).</summary>
    public void Quad(int a, int b, int c, int d)
    {
        Triangle(a, b, c);
        Triangle(a, c, d);
    }

    /// <summary>Finishes the mesh: heights are measured from its lowest point.</summary>
    public MeshData Finish()
    {
        float bottom = _mesh.Positions.Count > 0 ? float.MaxValue : 0f;
        foreach (Vector3 p in _mesh.Positions)
        {
            bottom = MathF.Min(bottom, p.Y);
        }

        for (int i = 0; i < _mesh.Positions.Count; i++)
        {
            _mesh.Heights[i] = _mesh.Positions[i].Y - bottom;
        }

        return _mesh;
    }
}
