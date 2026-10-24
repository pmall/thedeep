using Godot;

namespace TheDeep;

/// <summary>Part: a plate hanging down -Y from its origin, broad face toward +Z, tapering and curving (armor, fins, blades).</summary>
[GlobalClass]
public partial class PlateMesh : KitMesh
{
    private float _width = 0.2f;
    private float _length = 0.3f;
    private float _thickness = 0.02f;
    private float _taper = 1f;
    private float _curve;
    private float _bevel = 0.006f;

    [Export] public float Width { get => _width; set { _width = value; RequestUpdate(); } }
    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }
    [Export] public float Thickness { get => _thickness; set { _thickness = value; RequestUpdate(); } }

    /// <summary>Width at the far end as a share of <see cref="Width"/>.</summary>
    [Export] public float Taper { get => _taper; set { _taper = value; RequestUpdate(); } }

    /// <summary>Bend toward +Z over the length, degrees.</summary>
    [Export] public float Curve { get => _curve; set { _curve = value; RequestUpdate(); } }

    [Export] public float Bevel { get => _bevel; set { _bevel = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        PartShapes.Plate(builder, Width, Length, Thickness, Taper, Mathf.DegToRad(Curve), Bevel);
}
