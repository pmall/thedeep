using Godot;

namespace TheDeep;

/// <summary>Prop piece: the Harpoon's sight, wedges round a ring pointing at its middle (<see cref="AnchorShapes.Sight"/>).</summary>
[GlobalClass]
public partial class SightMesh : KitMesh
{
    private float _radius = 0.6f;
    private int _marks = 4;
    private float _size = 0.16f;
    private float _depth = 0.03f;

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public int Marks { get => _marks; set { _marks = value; RequestUpdate(); } }
    [Export] public float Size { get => _size; set { _size = value; RequestUpdate(); } }
    [Export] public float Depth { get => _depth; set { _depth = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => AnchorShapes.Sight(builder, Radius, Marks, Size, Depth);
}
