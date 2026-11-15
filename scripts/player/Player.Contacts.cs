using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

// What the body touches: its contacts with metal, rays and the fit of a pose.
public partial class Player
{
    /// <summary>The dot product under which a surface's normal faces the way the body heads (<see cref="MetalContact"/>): more than 60 degrees past square-on.</summary>
    public const float HeadOn = -0.5f;

    /// <summary>
    /// A contact from the last move with a metal surface whose (axis-snapped) normal has a dot product with <paramref name="heading"/>
    /// under <paramref name="maxDot"/> (<see cref="HeadOn"/>: the surface is in the way of that heading).
    /// </summary>
    public (NVector2 Normal, NVector2 Point)? MetalContact(NVector2 heading, float maxDot)
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            using KinematicCollision3D collision = GetSlideCollision(i); // disposed: a live wrapper makes the engine allocate a new one next call
            NVector2 normal = GripMath.Snap(collision.GetNormal().Flat());
            if (collision.GetCollider() is CollisionObject3D body && (body.CollisionLayer & PhysicsLayers.WorldMetal) != 0 && NVector2.Dot(normal, heading) < maxDot)
            {
                return (normal, collision.GetPosition().Flat());
            }
        }

        return null;
    }

    /// <summary>What a ray on the play plane first hits: metal, rock, or null (nothing).</summary>
    public SurfaceKind? SurfaceRay(NVector2 from, NVector2 to)
    {
        if (!Ray.Cast(from.OnPlane(), to.OnPlane()) || Ray.Body is not { } body)
        {
            return null;
        }

        return (body.CollisionLayer & PhysicsLayers.WorldMetal) != 0 ? SurfaceKind.Metal : SurfaceKind.Rock;
    }

    /// <summary>Where a ray on the play plane first hits metal, or null (rock, or nothing, in the way).</summary>
    public NVector2? MetalRay(NVector2 from, NVector2 to)
    {
        if (!Ray.Cast(from.OnPlane(), to.OnPlane()) || Ray.Body is not { } body
            || (body.CollisionLayer & PhysicsLayers.WorldMetal) == 0)
        {
            return null;
        }

        return Ray.GetCollisionPoint().Flat();
    }

    /// <summary>Whether the body fits at <paramref name="pose"/> (tested a little off the surface it stands on).</summary>
    private bool Fits(GripPose pose)
    {
        var basis = new Basis(Vector3.Back, GripMath.Angle(pose.Up));
        var origin = new Vector3(pose.Feet.X + pose.Up.X * Tuning.FitLift, pose.Feet.Y + pose.Up.Y * Tuning.FitLift, 0f);
        return !Fit.Overlaps(new Transform3D(basis, origin) * new Transform3D(Basis.Identity, CapsuleOffset));
    }

    /// <summary>How far above the feet the floor ray starts (meters): inside the body, so a floor that rose a little is still hit.</summary>
    private const float FloorProbeUp = 0.1f;

    /// <summary>How far below the feet the floor ray ends (meters): past the contact skin, short of any floor the body isn't on.</summary>
    private const float FloorProbeDown = 0.3f;

    /// <summary>
    /// The surface under the feet, from the physics layer of the floor a short ray straight down hits (a ray allocates nothing, where
    /// the slide collisions would wrap one every tick). Where the ray finds none (standing on a ledge's very edge), the last one holds.
    /// </summary>
    private SurfaceKind FloorSurfaceKind()
    {
        NVector2 feet = Feet;
        return SurfaceRay(feet + new NVector2(0f, FloorProbeUp), feet - new NVector2(0f, FloorProbeDown)) ?? FloorSurface ?? SurfaceKind.Rock;
    }
}
