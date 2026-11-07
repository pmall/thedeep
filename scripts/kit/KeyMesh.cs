using Godot;

namespace TheDeep;

/// <summary>Prop piece: a key standing along Y, its middle at the origin, its face toward the camera (<see cref="PropShapes.Key"/>).</summary>
[GlobalClass]
public partial class KeyMesh : KitMesh
{
    private float _length = 0.6f;
    private float _bowRadius = 0.13f;
    private float _thickness = 0.04f;

    /// <summary>From the bow's top to the shaft's foot, meters.</summary>
    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }

    [Export] public float BowRadius { get => _bowRadius; set { _bowRadius = value; RequestUpdate(); } }

    /// <summary>How thick the bow's ring and the bit are, meters.</summary>
    [Export] public float Thickness { get => _thickness; set { _thickness = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PropShapes.Key(builder, Length, BowRadius, Thickness);
}
