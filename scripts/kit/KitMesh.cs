using Godot;
using Godot.Collections;
using NVector3 = System.Numerics.Vector3;

namespace TheDeep;

/// <summary>
/// Base of the environment kit's procedural meshes: a <see cref="PrimitiveMesh"/> whose arrays come from
/// <see cref="MeshBuilder"/> (pure geometry in <see cref="KitShapes"/>). Being a mesh resource, a piece can
/// be swapped for an authored mesh in a scene without touching anything else; the material only needs
/// the surface frame (assets/shaders/lib/surface.gdshaderinc):
/// UV = position on the patch (m), UV2 = patch size (m, negative = wraps), TANGENT = +U with the +V sign,
/// COLOR.r = height above the piece's bottom / <see cref="HeightRange"/>, COLOR.g = per-part variation.
/// </summary>
public partial class KitMesh : PrimitiveMesh
{
    /// <summary>Heights are stored in 8-bit vertex color, over this range in meters (about 3 cm steps).</summary>
    public const float HeightRange = 8f;

    public override Array _CreateMeshArray()
    {
        var builder = new MeshBuilder();
        Build(builder);
        return ToArrays(builder.Finish());
    }

    /// <summary>Godot mesh arrays for <paramref name="mesh"/>, with the surface frame the materials read.</summary>
    public static Array ToArrays(MeshData mesh) => ToArrays(mesh, new Buffers());

    /// <summary>
    /// Kept buffers for <see cref="ToArrays(MeshData, Buffers)"/>, for a mesh rebuilt every frame with the same topology (a chain):
    /// the managed arrays and the Godot array are reused, so nothing is left for the garbage collector.
    /// </summary>
    public sealed class Buffers
    {
        public Vector3[] Positions = System.Array.Empty<Vector3>();
        public Vector3[] Normals = System.Array.Empty<Vector3>();
        public float[] Tangents = System.Array.Empty<float>();
        public Color[] Colors = System.Array.Empty<Color>();
        public Vector2[] Uvs = System.Array.Empty<Vector2>();
        public Vector2[] Patches = System.Array.Empty<Vector2>();
        public int[] Indices = System.Array.Empty<int>();
        public readonly Array Arrays = new();

        public void Fit(int vertices, int indices)
        {
            if (Positions.Length != vertices)
            {
                Positions = new Vector3[vertices];
                Normals = new Vector3[vertices];
                Tangents = new float[vertices * 4];
                Colors = new Color[vertices];
                Uvs = new Vector2[vertices];
                Patches = new Vector2[vertices];
            }

            if (Indices.Length != indices)
            {
                Indices = new int[indices];
            }

            if (Arrays.Count != (int)ArrayType.Max)
            {
                Arrays.Resize((int)ArrayType.Max);
            }
        }
    }

    /// <summary>Godot mesh arrays for <paramref name="mesh"/>, written into <paramref name="buffers"/> (returns its Godot array).</summary>
    public static Array ToArrays(MeshData mesh, Buffers buffers)
    {
        int count = mesh.VertexCount;
        buffers.Fit(count, mesh.Indices.Count);
        Vector3[] positions = buffers.Positions;
        Vector3[] normals = buffers.Normals;
        float[] tangents = buffers.Tangents;
        Color[] colors = buffers.Colors;
        Vector2[] uvs = buffers.Uvs;
        Vector2[] patches = buffers.Patches;
        for (int i = 0; i < count; i++)
        {
            positions[i] = ToGodot(mesh.Positions[i]);
            normals[i] = ToGodot(mesh.Normals[i]);
            System.Numerics.Vector4 t = mesh.Tangents[i];
            tangents[i * 4] = t.X;
            tangents[i * 4 + 1] = t.Y;
            tangents[i * 4 + 2] = t.Z;
            tangents[i * 4 + 3] = t.W;
            colors[i] = new Color(Mathf.Clamp(mesh.Heights[i] / HeightRange, 0f, 1f), mesh.Variations[i], 0f);
            uvs[i] = new Vector2(mesh.Uvs[i].X, mesh.Uvs[i].Y);
            patches[i] = new Vector2(mesh.PatchSizes[i].X, mesh.PatchSizes[i].Y);
        }

        mesh.Indices.CopyTo(buffers.Indices);
        Array arrays = buffers.Arrays;
        arrays[(int)ArrayType.Vertex] = positions;
        arrays[(int)ArrayType.Normal] = normals;
        arrays[(int)ArrayType.Tangent] = tangents;
        arrays[(int)ArrayType.Color] = colors;
        arrays[(int)ArrayType.TexUV] = uvs;
        arrays[(int)ArrayType.TexUV2] = patches;
        arrays[(int)ArrayType.Index] = buffers.Indices;
        return arrays;
    }

    /// <summary>Writes the piece's geometry. Exported properties call <see cref="PrimitiveMesh.RequestUpdate"/> when set.</summary>
    protected virtual void Build(MeshBuilder builder)
    {
    }

    protected static NVector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);

    private static Vector3 ToGodot(NVector3 v) => new(v.X, v.Y, v.Z);
}
