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
    /// <summary>A new ray under <paramref name="owner"/> (which must be in the tree to cast), hitting bodies on <paramref name="mask"/>.</summary>
    public static PlaneRay AddTo(Node owner, uint mask)
    {
        var ray = new PlaneRay
        {
            Name = nameof(PlaneRay),
            TopLevel = true,
            Enabled = false,
            ExcludeParent = false,
            CollisionMask = mask,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        };
        owner.AddChild(ray, false, InternalMode.Back);
        return ray;
    }

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
