using Godot;

namespace TheDeep;

/// <summary>Kit piece: a bank of an old machine's organ pipes on a wind chest, standing on its origin (<see cref="KitShapes.Bank"/>).</summary>
[GlobalClass]
public partial class BankMesh : KitMesh
{
    private Vector3 _size = new(3f, 5f, 0.8f);
    private float _chest = 0.6f;
    private int _seed = 1;

    [Export] public Vector3 Size { get => _size; set { _size = value; RequestUpdate(); } }

    /// <summary>The wind chest's height, meters.</summary>
    [Export] public float Chest { get => _chest; set { _chest = value; RequestUpdate(); } }

    [Export] public int Seed { get => _seed; set { _seed = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => KitShapes.Bank(builder, Size.ToCore(), Chest, Seed);
}
