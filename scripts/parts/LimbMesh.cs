using Godot;

namespace TheDeep;

/// <summary>
/// Part: a limb segment's frame hanging from its joint along <see cref="Axis"/>: a strut, the joint's housing,
/// a piston, cables (<see cref="LimbFrame"/>, <see cref="LimbShapes.Frame"/>). Pair it with a
/// <see cref="SleeveMesh"/> on the same joint for an armored limb. Sizes in meters.
/// </summary>
[GlobalClass]
public partial class LimbMesh : KitMesh
{
    private Vector3 _axis = Vector3.Down;
    private float _length = 0.4f;
    private float _strut = 0.05f;
    private float _strutTaper = 0.8f;
    private float _knuckle = 0.05f;
    private float _knuckleDepth = 0.1f;
    private float _endKnuckle;
    private float _piston;
    private float _pistonSide;
    private int _cables;
    private float _cableWidth = 0.012f;
    private float _cableSide;

    [Export] public Vector3 Axis { get => _axis; set { _axis = value; RequestUpdate(); } }
    [Export] public float Length { get => _length; set { _length = value; RequestUpdate(); } }

    /// <summary>The strut's width at the joint.</summary>
    [Export] public float Strut { get => _strut; set { _strut = value; RequestUpdate(); } }

    /// <summary>The strut's width at its far end, a share of <see cref="Strut"/>.</summary>
    [Export] public float StrutTaper { get => _strutTaper; set { _strutTaper = value; RequestUpdate(); } }

    /// <summary>Radius of the joint's housing at the origin (its axle faces the camera); 0 for none.</summary>
    [Export] public float Knuckle { get => _knuckle; set { _knuckle = value; RequestUpdate(); } }

    [Export] public float KnuckleDepth { get => _knuckleDepth; set { _knuckleDepth = value; RequestUpdate(); } }

    /// <summary>Radius of a smaller housing at the far end (a wrist, an ankle); 0 for none.</summary>
    [Export] public float EndKnuckle { get => _endKnuckle; set { _endKnuckle = value; RequestUpdate(); } }

    /// <summary>Radius of a piston's barrel; 0 for none.</summary>
    [Export] public float Piston { get => _piston; set { _piston = value; RequestUpdate(); } }

    /// <summary>How far across the strut the piston runs, in the camera's plane: positive is forward of a hanging limb.</summary>
    [Export] public float PistonSide { get => _pistonSide; set { _pistonSide = value; RequestUpdate(); } }

    [Export] public int Cables { get => _cables; set { _cables = value; RequestUpdate(); } }
    [Export] public float CableWidth { get => _cableWidth; set { _cableWidth = value; RequestUpdate(); } }

    /// <summary>How far across the strut the cables bow at their middle (signed as <see cref="PistonSide"/>).</summary>
    [Export] public float CableSide { get => _cableSide; set { _cableSide = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        LimbShapes.Frame(builder, Axis.ToCore(), Length, new LimbFrame
        {
            Strut = Strut, StrutTaper = StrutTaper, Knuckle = Knuckle, KnuckleDepth = KnuckleDepth, EndKnuckle = EndKnuckle,
            Piston = Piston, PistonSide = PistonSide, Cables = Cables, CableWidth = CableWidth, CableSide = CableSide,
        });
}
