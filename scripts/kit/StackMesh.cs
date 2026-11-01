using Godot;

namespace TheDeep;

/// <summary>Kit piece: a stack of shelves standing on its origin (<see cref="KitShapes.Stack"/>), cheap to repeat far away.</summary>
[GlobalClass]
public partial class StackMesh : KitMesh
{
    private Vector3 _size = new(2f, 6f, 2f);
    private float _shelf = 1.2f;

    [Export] public Vector3 Size { get => _size; set { _size = value; RequestUpdate(); } }

    /// <summary>Height between shelves, meters.</summary>
    [Export] public float Shelf { get => _shelf; set { _shelf = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => KitShapes.Stack(builder, ToNumerics(Size), Shelf);
}
