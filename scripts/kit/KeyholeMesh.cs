using Godot;

namespace TheDeep;

/// <summary>
/// Prop piece: a keyhole facing the camera, its back on the origin's plane and the eye's middle at the origin, a locked door's mark.
/// <see cref="Height"/> is the whole shape's height, <see cref="Depth"/> its thickness (<see cref="PropShapes.Keyhole"/>).
/// </summary>
[GlobalClass]
public partial class KeyholeMesh : KitMesh
{
    private float _height = 0.2f;
    private float _depth = 0.02f;

    [Export] public float Height { get => _height; set { _height = value; RequestUpdate(); } }
    [Export] public float Depth { get => _depth; set { _depth = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PropShapes.Keyhole(builder, Height, Depth);
}
