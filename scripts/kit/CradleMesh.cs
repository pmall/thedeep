using Godot;

namespace TheDeep;

/// <summary>
/// Prop piece: the claws of a reliquary's cradle, standing on the origin (a pedestal's top) with an item floating between them.
/// <see cref="Radius"/> is the collar's width, <see cref="Height"/> where the prongs turn back in, <see cref="Prongs"/> how many (one points away from the camera) (<see cref="PropShapes.Cradle"/>).
/// </summary>
[GlobalClass]
public partial class CradleMesh : KitMesh
{
    private float _radius = 0.22f;
    private float _height = 0.4f;
    private int _prongs = 3;

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public float Height { get => _height; set { _height = value; RequestUpdate(); } }
    [Export] public int Prongs { get => _prongs; set { _prongs = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PropShapes.Cradle(builder, Radius, Height, Prongs);
}
