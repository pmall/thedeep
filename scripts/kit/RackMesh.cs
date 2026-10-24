using Godot;

namespace TheDeep;

/// <summary>Kit piece: a server rack of <see cref="Size"/> standing on its origin, front facing +Z.</summary>
[GlobalClass]
public partial class RackMesh : KitMesh
{
    private Vector3 _size = new(1f, 2.4f, 1f);
    private int _units = 12;
    private int _seed = 1;

    [Export] public Vector3 Size { get => _size; set { _size = value; RequestUpdate(); } }

    /// <summary>Drive bays from bottom to top.</summary>
    [Export] public int Units { get => _units; set { _units = value; RequestUpdate(); } }

    /// <summary>Which bays are pulled out or missing.</summary>
    [Export] public int Seed { get => _seed; set { _seed = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => KitShapes.Rack(builder, ToNumerics(Size), Units, Seed);
}
