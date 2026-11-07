using Godot;

namespace TheDeep;

/// <summary>Prop piece: the prongs of a reliquary's cradle on a pedestal's top, an item floating between them (<see cref="PropShapes.Cradle"/>).</summary>
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
