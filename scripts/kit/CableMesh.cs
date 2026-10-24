using Godot;

namespace TheDeep;

/// <summary>Kit piece: cables hanging from the origin to <see cref="End"/>.</summary>
[GlobalClass]
public partial class CableMesh : KitMesh
{
    private Vector3 _end = new(6f, 0f, 0f);
    private float _sag = 1f;
    private float _radius = 0.035f;
    private int _strands = 1;
    private int _seed = 1;

    [Export] public Vector3 End { get => _end; set { _end = value; RequestUpdate(); } }

    /// <summary>How far the first cable dips at its middle, meters.</summary>
    [Export] public float Sag { get => _sag; set { _sag = value; RequestUpdate(); } }

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public int Strands { get => _strands; set { _strands = value; RequestUpdate(); } }
    [Export] public int Seed { get => _seed; set { _seed = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        KitShapes.Cables(builder, ToNumerics(End), Sag, Radius, Strands, Seed);
}
