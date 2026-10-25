using Godot;

namespace TheDeep;

/// <summary>Part: a fluted drill bit pointing down -Y from its collar; see <see cref="PartShapes.Drill"/>.</summary>
[GlobalClass]
public partial class DrillMesh : KitMesh
{
    private float _length = 0.4f;
    private float _radius = 0.08f;
    private int _flutes = 2;
    private float _turns = 2.5f;

    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }
    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public int Flutes { get => _flutes; set { _flutes = value; RequestUpdate(); } }
    [Export] public float Turns { get => _turns; set { _turns = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) => PartShapes.Drill(builder, Length, Radius, Flutes, Turns);
}
