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
    public static Array ToArrays(MeshData mesh)
    {
        int count = mesh.VertexCount;
        var positions = new Vector3[count];
        var normals = new Vector3[count];
        var tangents = new float[count * 4];
        var colors = new Color[count];
        var uvs = new Vector2[count];
        var patches = new Vector2[count];
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

        var arrays = new Array();
        arrays.Resize((int)ArrayType.Max);
        arrays[(int)ArrayType.Vertex] = positions;
        arrays[(int)ArrayType.Normal] = normals;
        arrays[(int)ArrayType.Tangent] = tangents;
        arrays[(int)ArrayType.Color] = colors;
        arrays[(int)ArrayType.TexUV] = uvs;
        arrays[(int)ArrayType.TexUV2] = patches;
        arrays[(int)ArrayType.Index] = mesh.Indices.ToArray();
        return arrays;
    }

    /// <summary>Writes the piece's geometry. Exported properties call <see cref="PrimitiveMesh.RequestUpdate"/> when set.</summary>
    protected virtual void Build(MeshBuilder builder)
    {
    }

    protected static NVector3 ToNumerics(Vector3 v) => new(v.X, v.Y, v.Z);

    private static Vector3 ToGodot(NVector3 v) => new(v.X, v.Y, v.Z);
}
