using Godot;

namespace TheDeep;

/// <summary>
/// Part: plated armor around a limb segment, hanging from its joint along <see cref="Axis"/>: bands like a
/// gauntlet's lames, each flaring to its lower lip (<see cref="LimbSleeve"/>, <see cref="LimbShapes.Sleeve"/>).
/// Worn over a <see cref="LimbMesh"/> on the same joint. Sizes in meters; widths in the camera's plane, depths toward it.
/// </summary>
[GlobalClass]
public partial class SleeveMesh : KitMesh
{
    private Vector3 _axis = Vector3.Down;
    private float _length = 0.4f;
    private float _from = 0.1f;
    private float _to = 0.9f;
    private int _bands = 3;
    private float _widthTop = 0.12f;
    private float _widthBottom = 0.1f;
    private float _depthTop = 0.1f;
    private float _depthBottom = 0.09f;
    private float _flare = 0.18f;
    private float _overlap = 0.15f;
    private float _shift;
    private int _sides = 12;

    [Export] public Vector3 Axis { get => _axis; set { _axis = value; RequestUpdate(); } }
    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }

    /// <summary>Where the plating starts along the limb, a share of <see cref="Length"/>.</summary>
    [Export] public float From { get => _from; set { _from = value; RequestUpdate(); } }

    /// <summary>Where the plating ends along the limb, a share of <see cref="Length"/>.</summary>
    [Export] public float To { get => _to; set { _to = value; RequestUpdate(); } }

    [Export] public int Bands { get => _bands; set { _bands = value; RequestUpdate(); } }
    [Export] public float WidthTop { get => _widthTop; set { _widthTop = value; RequestUpdate(); } }
    [Export] public float WidthBottom { get => _widthBottom; set { _widthBottom = value; RequestUpdate(); } }
    [Export] public float DepthTop { get => _depthTop; set { _depthTop = value; RequestUpdate(); } }
    [Export] public float DepthBottom { get => _depthBottom; set { _depthBottom = value; RequestUpdate(); } }

    /// <summary>How much wider a band's lower lip is than its top, a share of its width.</summary>
    [Export] public float Flare { get => _flare; set { _flare = value; RequestUpdate(); } }

    /// <summary>The share of a band the next one tucks under (negative leaves a gap).</summary>
    [Export] public float Overlap { get => _overlap; set { _overlap = value; RequestUpdate(); } }

    /// <summary>Moves the plating across the limb in the camera's plane (positive is forward of a hanging limb).</summary>
    [Export] public float Shift { get => _shift; set { _shift = value; RequestUpdate(); } }

    [Export] public int Sides { get => _sides; set { _sides = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        LimbShapes.Sleeve(builder, Axis.ToCore(), Length, new LimbSleeve
        {
            From = From, To = To, Bands = Bands, WidthTop = WidthTop, WidthBottom = WidthBottom,
            DepthTop = DepthTop, DepthBottom = DepthBottom, Flare = Flare, Overlap = Overlap, Shift = Shift, Sides = Sides,
        });
}
