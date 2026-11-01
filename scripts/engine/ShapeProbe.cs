using Godot;

namespace TheDeep;

/// <summary>
/// A shape overlap test that allocates nothing (see <see cref="PlaneRay"/>): a top-level <see cref="ShapeCast3D"/>
/// with no motion, tested on demand by <see cref="Overlaps"/>.
/// </summary>
public partial class ShapeProbe : ShapeCast3D
{
    /// <summary>A new probe against bodies on <paramref name="mask"/>; it tests once <see cref="Attach"/>ed with its shape.</summary>
    public static ShapeProbe For(uint mask) => new()
    {
        Name = nameof(ShapeProbe),
        TopLevel = true,
        Enabled = false,
        ExcludeParent = false,
        TargetPosition = Vector3.Zero,
        Margin = 0f,
        MaxResults = 1,
        CollisionMask = mask,
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
    };

    /// <summary>Puts the probe under <paramref name="owner"/> (an internal child), testing <paramref name="shape"/>.</summary>
    public void Attach(Node owner, Shape3D shape)
    {
        Shape = shape;
        owner.AddChild(this, false, InternalMode.Back);
    }

    /// <summary>Whether the shape placed at <paramref name="transform"/> (global) overlaps anything.</summary>
    public bool Overlaps(Transform3D transform)
    {
        GlobalTransform = transform;
        ForceShapecastUpdate();
        return IsColliding();
    }
}
