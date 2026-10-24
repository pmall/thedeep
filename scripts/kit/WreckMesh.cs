using Godot;

namespace TheDeep;

/// <summary>Kit piece: a heap of broken machinery about <see cref="Size"/> meters across, one per <see cref="Seed"/>.</summary>
[GlobalClass]
public partial class WreckMesh : KitMesh
{
    private float _size = 3f;
    private int _seed = 1;

    [Export] public float Size { get => _size; set { _size = value; RequestUpdate(); } }
    [Export] public int Seed { get => _seed; set { _seed = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => KitShapes.Wreck(builder, Size, Seed);
}
