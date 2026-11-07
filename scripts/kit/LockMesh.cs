using Godot;

namespace TheDeep;

/// <summary>
/// Prop piece: a key lock's round boss, facing the camera with its back on the origin's plane: housing, rim and studs
/// (<see cref="PropShapes.Boss"/>). A <see cref="KeyholeMesh"/> sits in its face.
/// </summary>
[GlobalClass]
public partial class LockMesh : KitMesh
{
    private float _radius = 0.25f;
    private float _depth = 0.08f;
    private int _studs = 8;

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public float Depth { get => _depth; set { _depth = value; RequestUpdate(); } }
    [Export] public int Studs { get => _studs; set { _studs = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PropShapes.Boss(builder, Radius, Depth, Studs);
}
