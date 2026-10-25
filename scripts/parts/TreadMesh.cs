using Godot;

namespace TheDeep;

/// <summary>
/// Part: a track loop with its road wheels, resting on its origin; see <see cref="PartShapes.Tread"/>.
/// The belt is one wrapping patch, so the machine material scrolls cleats along it as the machine moves.
/// </summary>
[GlobalClass]
public partial class TreadMesh : KitMesh
{
    private float _length = 0.6f;
    private float _radius = 0.14f;
    private float _width = 0.14f;
    private float _thickness = 0.035f;
    private int _wheels = 3;

    /// <summary>Distance between the end wheels' centers, meters.</summary>
    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }

    /// <summary>Height of the end wheels' centers (the loop's half height), meters.</summary>
    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }

    [Export] public float Width { get => _width; set { _width = value; RequestUpdate(); } }
    [Export] public float Thickness { get => _thickness; set { _thickness = value; RequestUpdate(); } }
    [Export] public int Wheels { get => _wheels; set { _wheels = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        PartShapes.Tread(builder, Length, Radius, Width, Thickness, Wheels);
}
