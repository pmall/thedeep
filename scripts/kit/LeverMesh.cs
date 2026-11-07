using Godot;

namespace TheDeep;

/// <summary>
/// Prop piece: a lever's housing standing on the origin: plinth, cheeks and drum at the pivot, the quadrant over it
/// (<see cref="PropShapes.LeverBase"/>). Its handle (<see cref="LeverHandleMesh"/>) turns at the pivot.
/// </summary>
[GlobalClass]
public partial class LeverMesh : KitMesh
{
    private Vector3 _size = new(0.62f, 0.34f, 0.5f);
    private float _pivotHeight = 0.5f;
    private float _guideRadius = 0.55f;
    private float _guideAngle = 45f;

    /// <summary>The plinth: width, height, depth, meters.</summary>
    [Export] public Vector3 Size { get => _size; set { _size = value; RequestUpdate(); } }

    [Export] public float PivotHeight { get => _pivotHeight; set { _pivotHeight = value; RequestUpdate(); } }

    /// <summary>Radius of the quadrant over the pivot, meters.</summary>
    [Export] public float GuideRadius { get => _guideRadius; set { _guideRadius = value; RequestUpdate(); } }

    /// <summary>How far the quadrant reaches either side of upright, degrees.</summary>
    [Export] public float GuideAngle { get => _guideAngle; set { _guideAngle = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        PropShapes.LeverBase(builder, Size.ToCore(), PivotHeight, GuideRadius, Mathf.DegToRad(GuideAngle));
}
