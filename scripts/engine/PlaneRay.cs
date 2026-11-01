using Godot;

namespace TheDeep;

/// <summary>
/// A ray (or a shape overlap) cast on demand that allocates nothing. <c>PhysicsDirectSpaceState3D.IntersectRay</c>
/// builds a query object and returns an engine dictionary on every call, both finalizable garbage; per-tick
/// callers (the pilgrim, the harpoon's sight, the machines) keep one of these instead. It is a top-level
/// <see cref="RayCast3D"/> that never casts by itself: <see cref="Cast"/> moves it and casts at once.
/// </summary>
public partial class PlaneRay : RayCast3D
{
    /// <summary>A new ray hitting bodies on <paramref name="mask"/>; it casts once <see cref="Attach"/>ed to a node in the tree.</summary>
    public static PlaneRay For(uint mask) => new()
    {
        Name = nameof(PlaneRay),
        TopLevel = true,
        Enabled = false,
        ExcludeParent = false,
        CollisionMask = mask,
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
    };

    /// <summary>Puts the ray under <paramref name="owner"/> (an internal child: not part of its scene).</summary>
    public void Attach(Node owner) => owner.AddChild(this, false, InternalMode.Back);

    /// <summary>Casts from <paramref name="from"/> to <paramref name="to"/> (global); whether it hit anything.</summary>
    public bool Cast(Vector3 from, Vector3 to)
    {
        GlobalTransform = new Transform3D(Basis.Identity, from);
        TargetPosition = to - from;
        ForceRaycastUpdate();
        return IsColliding();
    }

    /// <summary>The body the last cast hit, or null.</summary>
    public CollisionObject3D? Body => IsColliding() ? GetCollider() as CollisionObject3D : null;
}
