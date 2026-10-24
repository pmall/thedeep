using Godot;

namespace TheDeep;

/// <summary>Kit piece: a grate of <see cref="Size"/> (X wide, Y thick, Z deep) standing on its origin.</summary>
[GlobalClass]
public partial class GrateMesh : KitMesh
{
    private Vector3 _size = new(4f, 0.25f, 3f);
    private float _spacing = 0.16f;
    private float _bar = 0.04f;

    [Export] public Vector3 Size { get => _size; set { _size = value; RequestUpdate(); } }

    /// <summary>Distance between bars, meters.</summary>
    [Export] public float Spacing { get => _spacing; set { _spacing = value; RequestUpdate(); } }

    [Export] public float Bar { get => _bar; set { _bar = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => KitShapes.Grate(builder, ToNumerics(Size), Spacing, Bar);
}
