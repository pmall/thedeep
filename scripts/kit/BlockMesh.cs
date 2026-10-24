using Godot;

namespace TheDeep;

/// <summary>Kit piece: a block of <see cref="Size"/> centered on its origin, with bevelled edges.</summary>
[GlobalClass]
public partial class BlockMesh : KitMesh
{
    private Vector3 _size = Vector3.One;
    private float _bevel = 0.05f;

    [Export] public Vector3 Size { get => _size; set { _size = value; RequestUpdate(); } }

    /// <summary>Chamfer on every edge, meters.</summary>
    [Export] public float Bevel { get => _bevel; set { _bevel = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => KitShapes.Block(builder, ToNumerics(Size), Bevel);
}
