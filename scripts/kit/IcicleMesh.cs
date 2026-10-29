using Godot;

namespace TheDeep;

/// <summary>Kit piece: a fringe of icicles hanging from its origin, <see cref="Width"/> wide, the longest <see cref="Length"/>.</summary>
[GlobalClass]
public partial class IcicleMesh : KitMesh
{
    private float _width = 3f;
    private float _length = 1.5f;
    private int _count = 9;
    private int _seed = 1;
    private float _spread = 1f;

    [Export] public float Width { get => _width; set { _width = value; RequestUpdate(); } }
    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }
    [Export] public int Count { get => _count; set { _count = value; RequestUpdate(); } }
    [Export] public int Seed { get => _seed; set { _seed = value; RequestUpdate(); } }

    /// <summary>1: mostly short ones, now and then a long one; 0: all full length (a frozen fall).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Spread { get => _spread; set { _spread = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => KitShapes.Icicles(builder, Width, Length, Count, Seed, Spread);
}
