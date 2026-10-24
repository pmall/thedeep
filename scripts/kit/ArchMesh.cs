using Godot;

namespace TheDeep;

/// <summary>
/// Kit piece: an arch ring in the XY plane springing from (±Span/2, 0): round up to Rise = Span/2,
/// pointed (Gothic) above. With a <see cref="Flange"/> it reads as an iron cathedral rib.
/// </summary>
[GlobalClass]
public partial class ArchMesh : KitMesh
{
    private float _span = 6f;
    private float _rise = 3f;
    private float _thickness = 0.6f;
    private float _depth = 1f;
    private float _flange;
    private int _segments = 32;

    [Export] public float Span { get => _span; set { _span = value; RequestUpdate(); } }
    [Export] public float Rise { get => _rise; set { _rise = value; RequestUpdate(); } }

    /// <summary>Width of the ring in the arch's plane, meters.</summary>
    [Export] public float Thickness { get => _thickness; set { _thickness = value; RequestUpdate(); } }

    /// <summary>Extent of the ring along Z, meters.</summary>
    [Export] public float Depth { get => _depth; set { _depth = value; RequestUpdate(); } }

    /// <summary>Height of a thin fin under the ring, meters; 0 for none.</summary>
    [Export] public float Flange { get => _flange; set { _flange = value; RequestUpdate(); } }

    [Export] public int Segments { get => _segments; set { _segments = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        KitShapes.Arch(builder, Span, Rise, Thickness, Depth, Flange, Segments);
}
