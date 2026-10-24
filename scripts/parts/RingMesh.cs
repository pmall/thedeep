using Godot;

namespace TheDeep;

/// <summary>Part: a band around the Y axis on an ellipse (a collar, a belt, a cuff; a visor when <see cref="Arc"/> is short).</summary>
[GlobalClass]
public partial class RingMesh : KitMesh
{
    private float _radiusX = 0.12f;
    private float _radiusZ = 0.12f;
    private float _height = 0.04f;
    private float _thickness = 0.015f;
    private float _arc = 360f;
    private int _segments = 32;

    /// <summary>Radius forward (X), meters.</summary>
    [Export] public float RadiusX { get => _radiusX; set { _radiusX = value; RequestUpdate(); } }

    /// <summary>Radius sideways (Z), meters.</summary>
    [Export] public float RadiusZ { get => _radiusZ; set { _radiusZ = value; RequestUpdate(); } }

    [Export] public float Height { get => _height; set { _height = value; RequestUpdate(); } }
    [Export] public float Thickness { get => _thickness; set { _thickness = value; RequestUpdate(); } }

    /// <summary>Degrees covered, centered on +X; 360 closes the ring.</summary>
    [Export] public float Arc { get => _arc; set { _arc = value; RequestUpdate(); } }

    [Export] public int Segments { get => _segments; set { _segments = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        PartShapes.Ring(builder, RadiusX, RadiusZ, Height, Thickness, Mathf.DegToRad(Arc), Segments);
}
