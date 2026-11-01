using Godot;

namespace TheDeep;

/// <summary>
/// A shape overlap test that allocates nothing (see <see cref="PlaneRay"/>): a top-level <see cref="ShapeCast3D"/>
/// with no motion, tested on demand by <see cref="Overlaps"/>.
/// </summary>
public partial class ShapeProbe : ShapeCast3D
{
    /// <summary>A new probe of <paramref name="shape"/> under <paramref name="owner"/>, against bodies on <paramref name="mask"/>.</summary>
    public static ShapeProbe AddTo(Node owner, Shape3D shape, uint mask)
    {
        var probe = new ShapeProbe
        {
            Name = nameof(ShapeProbe),
            TopLevel = true,
            Enabled = false,
            ExcludeParent = false,
            Shape = shape,
            TargetPosition = Vector3.Zero,
            Margin = 0f,
            MaxResults = 1,
            CollisionMask = mask,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        };
        owner.AddChild(probe, false, InternalMode.Back);
        return probe;
    }

    /// <summary>Whether the shape placed at <paramref name="transform"/> (global) overlaps anything.</summary>
    public bool Overlaps(Transform3D transform)
    {
        GlobalTransform = transform;
        ForceShapecastUpdate();
        return IsColliding();
    }
}
