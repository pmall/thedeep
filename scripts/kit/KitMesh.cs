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
/// COLOR.r = height above the piece's bottom / <see cref="HeightRange"/>, COLOR.g = per-part variation, COLOR.b = cavity.
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
    /// Buffers for <see cref="ToArrays(MeshData, Buffers)"/>. Kept (<c>new Buffers(kept: true)</c>) for a mesh rebuilt every frame with
    /// the same topology (a chain): the managed arrays are reused and the Godot array's slots written in place
    /// (<see cref="NativeMeshArrays"/>), so nothing is left for the garbage collector. Dispose kept buffers with their mesh.
    /// </summary>
    public sealed class Buffers : System.IDisposable
    {
        private readonly NativeMeshArrays? _native;

        /// <summary>Buffers used once, or with <paramref name="kept"/> reused frame after frame and written in place.</summary>
        public Buffers(bool kept = false)
        {
            _native = kept ? new NativeMeshArrays() : null;
            Arrays = _native?.Managed ?? new Array();
        }

        /// <summary>The Godot array the slots are written into (the native writer's own, for kept buffers).</summary>
        public Array Arrays { get; }

        /// <summary>The in-place writer of kept buffers; null for buffers used once.</summary>
        internal NativeMeshArrays? Native => _native;

        public void Dispose() => _native?.Dispose();

        /// <summary>Each vertex's position, in meters.</summary>
        public Vector3[] Positions { get; private set; } = System.Array.Empty<Vector3>();
        /// <summary>Each vertex's normal.</summary>
        public Vector3[] Normals { get; private set; } = System.Array.Empty<Vector3>();
        /// <summary>Each vertex's tangent, four floats a vertex (X, Y, Z and the binormal's sign).</summary>
        public float[] Tangents { get; private set; } = System.Array.Empty<float>();
        /// <summary>Each vertex's color.</summary>
        public Color[] Colors { get; private set; } = System.Array.Empty<Color>();
        /// <summary>Each vertex's texture coordinates.</summary>
        public Vector2[] Uvs { get; private set; } = System.Array.Empty<Vector2>();
        /// <summary>Each vertex's second texture coordinates (the mesh's UV2 slot).</summary>
        public Vector2[] Patches { get; private set; } = System.Array.Empty<Vector2>();
        /// <summary>The triangles, three vertex indices each.</summary>
        public int[] Indices { get; private set; } = System.Array.Empty<int>();

        /// <summary>Sizes the buffers for <paramref name="vertices"/> vertices and <paramref name="indices"/> indices, keeping
        /// those already that size.</summary>
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
            positions[i] = mesh.Positions[i].ToGodot();
            normals[i] = mesh.Normals[i].ToGodot();
            System.Numerics.Vector4 t = mesh.Tangents[i];
            tangents[i * 4] = t.X;
            tangents[i * 4 + 1] = t.Y;
            tangents[i * 4 + 2] = t.Z;
            tangents[i * 4 + 3] = t.W;
            colors[i] = new Color(Mathf.Clamp(mesh.Heights[i] / HeightRange, 0f, 1f), mesh.Variations[i], mesh.Cavities[i]);
            uvs[i] = mesh.Uvs[i].ToGodot();
            patches[i] = mesh.PatchSizes[i].ToGodot();
        }

        mesh.Indices.CopyTo(buffers.Indices);
        if (buffers.Native is { } native)
        {
            native.Set(ArrayType.Vertex, positions);
            native.Set(ArrayType.Normal, normals);
            native.Set(ArrayType.Tangent, tangents);
            native.Set(ArrayType.Color, colors);
            native.Set(ArrayType.TexUV, uvs);
            native.Set(ArrayType.TexUV2, patches);
            native.Set(ArrayType.Index, buffers.Indices);
            return native.Managed;
        }

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
}
