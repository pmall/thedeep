using Godot;

namespace TheDeep;

/// <summary>Prop piece: a lever's handle rising from its pivot at the origin along +Y (<see cref="PropShapes.LeverHandle"/>).</summary>
[GlobalClass]
public partial class LeverHandleMesh : KitMesh
{
    private float _length = 0.9f;
    private float _radius = 0.035f;

    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }
    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PropShapes.LeverHandle(builder, Length, Radius);
}
