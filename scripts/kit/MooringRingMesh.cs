using Godot;

namespace TheDeep;

/// <summary>Prop piece: a harpoon anchor's iron ring facing the camera, its collars and the shackle it hangs by (<see cref="AnchorShapes.Ring"/>).</summary>
[GlobalClass]
public partial class MooringRingMesh : KitMesh
{
    private float _radius = 0.48f;
    private float _thickness = 0.08f;
    private int _collars = 4;

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public float Thickness { get => _thickness; set { _thickness = value; RequestUpdate(); } }
    [Export] public int Collars { get => _collars; set { _collars = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => AnchorShapes.Ring(builder, Radius, Thickness, Collars);
}
