using Godot;

namespace TheDeep;

/// <summary>
/// Prop piece: a code's seal, a square plate with cut corners facing the camera, its back on the origin's plane, with a raised frame and a stud at each cut corner.
/// <see cref="Size"/> is the plate's width, <see cref="Depth"/> its thickness (<see cref="PropShapes.Seal"/>).
/// </summary>
[GlobalClass]
public partial class SealMesh : KitMesh
{
    private float _size = 0.42f;
    private float _depth = 0.07f;

    [Export] public float Size { get => _size; set { _size = value; RequestUpdate(); } }
    [Export] public float Depth { get => _depth; set { _depth = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PropShapes.Seal(builder, Size, Depth);
}
