using Godot;

namespace TheDeep;

/// <summary>Prop piece: a cog facing the camera, its back on the origin's plane, turning about its middle (<see cref="PropShapes.Cog"/>).</summary>
[GlobalClass]
public partial class CogMesh : KitMesh
{
    private float _radius = 0.25f;
    private int _teeth = 12;
    private float _depth = 0.07f;

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public int Teeth { get => _teeth; set { _teeth = value; RequestUpdate(); } }
    [Export] public float Depth { get => _depth; set { _depth = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PropShapes.Cog(builder, Radius, Teeth, Depth);
}
