using Godot;

namespace TheDeep;

/// <summary>
/// Prop piece: a flat band round a circle facing the camera, a harpoon anchor's lit hoop, set inside the iron ring's bar.
/// <see cref="Radius"/> is to the band's middle, <see cref="Width"/> its width and <see cref="Depth"/> its thickness; give it an emissive material (<see cref="AnchorShapes.Hoop"/>).
/// </summary>
[GlobalClass]
public partial class HoopMesh : KitMesh
{
    private float _radius = 0.4f;
    private float _width = 0.05f;
    private float _depth = 0.06f;

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public float Width { get => _width; set { _width = value; RequestUpdate(); } }
    [Export] public float Depth { get => _depth; set { _depth = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => AnchorShapes.Hoop(builder, Radius, Width, Depth);
}
