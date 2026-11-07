using Godot;

namespace TheDeep;

/// <summary>
/// Kit piece: a rank of organ pipes standing on its origin, side by side along X and centered, their mouths facing +Z
/// (<see cref="OrganPipeShape"/>): one pipe per entry of <see cref="Heights"/>, <see cref="Spacing"/> apart, the tallest
/// <see cref="Radius"/> wide.
/// </summary>
[GlobalClass]
public partial class OrganMesh : KitMesh
{
    private float[] _heights = { 4f, 5f, 6f, 5f, 4f };
    private float _spacing = 0.8f;
    private float _radius = 0.3f;

    [Export] public float[] Heights { get => _heights; set { _heights = value; RequestUpdate(); } }
    [Export] public float Spacing { get => _spacing; set { _spacing = value; RequestUpdate(); } }
    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => OrganPipeShape.Rank(builder, Heights, Spacing, Radius);
}
