using Godot;

namespace TheDeep;

/// <summary>
/// Kit piece: a curtain of frozen water hanging from its origin (<see cref="IceCurtainShape"/>), <see cref="Width"/> wide,
/// its longest flute <see cref="Length"/> down, bulging up to <see cref="Depth"/> toward +Z from its back at z = 0.
/// A frozen fall down a slope, meltwater frozen over a ledge.
/// </summary>
[GlobalClass]
public partial class IceCurtainMesh : KitMesh
{
    private float _width = 4f;
    private float _length = 6f;
    private float _depth = 0.8f;
    private int _seed = 1;
    private float _sheet = 0.62f;

    [Export] public float Width { get => _width; set { _width = value; RequestUpdate(); } }
    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }
    [Export] public float Depth { get => _depth; set { _depth = value; RequestUpdate(); } }
    [Export] public int Seed { get => _seed; set { _seed = value; RequestUpdate(); } }

    /// <summary>The share of the length the sheet between the flutes reaches: high for a fall frozen whole, low for drips over a lip.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Sheet { get => _sheet; set { _sheet = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => IceCurtainShape.Build(builder, Width, Length, Depth, Seed, Sheet);
}
