using Godot;

namespace TheDeep;

/// <summary>Prop piece: a flat-faceted crystal around Y, its band's middle at the origin (<see cref="PropShapes.Crystal"/>).</summary>
[GlobalClass]
public partial class CrystalMesh : KitMesh
{
    private float _radius = 0.15f;
    private int _sides = 6;
    private float _band = 0.1f;
    private float _top = 0.3f;
    private float _bottom = 0.2f;

    /// <summary>Radius at the band's corners, meters.</summary>
    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }

    [Export] public int Sides { get => _sides; set { _sides = value; RequestUpdate(); } }

    /// <summary>Height of the band of upright facets between the points, meters (0: two pyramids base to base).</summary>
    [Export] public float Band { get => _band; set { _band = value; RequestUpdate(); } }

    /// <summary>How far the upper point rises above the band, and the lower one sinks below it, meters.</summary>
    [Export] public float Top { get => _top; set { _top = value; RequestUpdate(); } }
    [Export] public float Bottom { get => _bottom; set { _bottom = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PropShapes.Crystal(builder, Radius, Sides, Band, Top, Bottom);
}
