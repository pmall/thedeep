using Godot;

namespace TheDeep;

/// <summary>
/// Prop piece: a harpoon anchor's iron ring facing the camera, with the collars clamped round it and the shackle it hangs by.
/// <see cref="Radius"/> is to the bar's middle, <see cref="Thickness"/> the bar's width, <see cref="Collars"/> how many collars sit on its diagonals;
/// the shackle's top is <see cref="AnchorShapes.ShackleTop"/> above the middle, where a rod or cable starts (<see cref="AnchorShapes.Ring"/>).
/// </summary>
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
