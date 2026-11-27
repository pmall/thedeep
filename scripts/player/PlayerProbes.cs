using System;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// World probes and physical ability checks for <see cref="Player"/>: metal contacts, surface rays, pose fit,
/// ledge-assist vault probing, Mag-grip attachment lookup and Drill rock boring. Keeps engine probe state and
/// collision sweeps off the pilgrim node itself.
/// </summary>
internal sealed class PlayerProbes : IDisposable
{
    /// <summary>How far above the feet the floor ray starts (meters): inside the body, so a floor that rose a little is still hit.</summary>
    private const float FloorProbeUp = 0.1f;

    /// <summary>How far below the feet the floor ray ends (meters): past the contact skin, short of any floor the body isn't on.</summary>
    private const float FloorProbeDown = 0.3f;

    private readonly Player _player;
    private readonly PlaneRay _ray = PlaneRay.For(PhysicsLayers.Solid);
    private readonly ShapeProbe _fit = ShapeProbe.For(PhysicsLayers.Solid);
    private readonly KinematicCollision3D _moveHit = new();

    public PlayerProbes(Player player) => _player = player;

    /// <inheritdoc/>
    public void Dispose() => _moveHit.Dispose();

    /// <summary>Binds the ray and fit probe to the pilgrim's body and capsule once the node is ready.</summary>
    public void Attach(CapsuleShape3D capsule)
    {
        _ray.Attach(_player);
        _fit.Attach(_player, capsule);
    }

    /// <summary>
    /// A contact from the last move with a metal surface whose (axis-snapped) normal has a dot product with <paramref name="heading"/>
    /// under <paramref name="maxDot"/> (<see cref="Player.HeadOn"/>: the surface is in the way of that heading).
    /// </summary>
    public (NVector2 Normal, NVector2 Point)? MetalContact(NVector2 heading, float maxDot)
    {
        for (int i = 0; i < _player.GetSlideCollisionCount(); i++)
        {
            using KinematicCollision3D collision = _player.GetSlideCollision(i); // disposed: a live wrapper makes the engine allocate a new one next call
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
        if (!_ray.Cast(from.OnPlane(), to.OnPlane()) || _ray.Body is not { } body)
        {
            return null;
        }

        return (body.CollisionLayer & PhysicsLayers.WorldMetal) != 0 ? SurfaceKind.Metal : SurfaceKind.Rock;
    }

    /// <summary>Where a ray on the play plane first hits metal, or null (rock, or nothing, in the way).</summary>
    public NVector2? MetalRay(NVector2 from, NVector2 to)
    {
        if (!_ray.Cast(from.OnPlane(), to.OnPlane()) || _ray.Body is not { } body
            || (body.CollisionLayer & PhysicsLayers.WorldMetal) == 0)
        {
            return null;
        }

        return _ray.GetCollisionPoint().Flat();
    }

    /// <summary>Whether the body fits at <paramref name="pose"/> (tested a little off the surface it stands on).</summary>
    public bool Fits(GripPose pose, Vector3 capsuleOffset)
    {
        var basis = new Basis(Vector3.Back, GripMath.Angle(pose.Up));
        var origin = new Vector3(pose.Feet.X + pose.Up.X * _player.Tuning.FitLift, pose.Feet.Y + pose.Up.Y * _player.Tuning.FitLift, 0f);
        return !_fit.Overlaps(new Transform3D(basis, origin) * new Transform3D(Basis.Identity, capsuleOffset));
    }

    /// <summary>
    /// The surface under the feet, from the physics layer of the floor a short ray straight down hits (a ray allocates nothing, where
    /// the slide collisions would wrap one every tick). Where the ray finds none (standing on a ledge's very edge), the last one holds.
    /// </summary>
    public SurfaceKind FloorSurfaceKind(NVector2 feet, SurfaceKind? fallback) =>
        SurfaceRay(feet + new NVector2(0f, FloorProbeUp), feet - new NVector2(0f, FloorProbeDown)) ?? fallback ?? SurfaceKind.Rock;

    /// <summary>Finds a valid Mag-grip attachment pose against a metal ceiling or wall from the pilgrim's current slide contacts.</summary>
    public GripPose? FindGripPose(Vector3 capsuleOffset)
    {
        GripPose? pose = null;
        if (_player.IsOnCeiling() && _player.Velocity.Y >= 0f && MetalContact(new NVector2(0f, 1f), Player.HeadOn) is { } ceiling)
        {
            pose = GripMath.OntoCeiling(_player.Feet, ceiling.Point.Y);
        }
        else if (_player.IsOnWall() && _player.CurrentInput.MoveX != 0
            && MetalContact(new NVector2(_player.CurrentInput.MoveX, 0f), Player.HeadOn) is { } wall)
        {
            pose = GripMath.OntoWall(_player.Feet, _player.CenterHeight, wall.Point.X, wall.Normal);
        }

        return pose is { } p && Fits(p, capsuleOffset) ? p : null;
    }

    /// <summary>
    /// Probes for a ledge-assist vault after an airborne move (<see cref="LedgeAssist"/>): returns the upright landing pose and
    /// preserved horizontal velocity, or null if no assist applies.
    /// </summary>
    public (GripPose Pose, Vector3 Velocity)? FindLedgeVault(float pushSpeed, float bodyRadius, Vector3 capsuleOffset)
    {
        int dir = _player.CurrentInput.MoveX;
        if (!_player.IsUpright || dir == 0 || !_player.IsOnWall() || _player.GetWallNormal().X * dir > -0.5f || Mathf.Sign(pushSpeed) != dir)
        {
            return null;
        }

        // The top of the wall, looked for from above the reach, just past its face.
        Vector3 position = _player.GlobalPosition;
        float probeX = position.X + dir * (bodyRadius + LedgeAssist.Reach);
        float assist = _player.Tuning.LedgeAssistHeight;
        if (!_ray.Cast(new Vector3(probeX, position.Y + assist + 0.05f, 0f), new Vector3(probeX, position.Y, 0f))
            || _ray.GetCollisionNormal().Y < 0.5f)
        {
            return null;
        }

        float top = _ray.GetCollisionPoint().Y;
        if (top - position.Y > assist)
        {
            return null;
        }

        var pose = new GripPose(new NVector2(position.X + dir * LedgeAssist.Reach, top + 0.01f), new NVector2(0f, 1f));
        if (!Fits(pose, capsuleOffset))
        {
            return null;
        }

        return (pose, new Vector3(dir * LedgeAssist.Speed(pushSpeed, _player.Tuning.RunSpeed), 0f, 0f));
    }

    /// <summary>The unbroken cracked rock the body would hit first moving by <paramref name="motion"/>, or null.</summary>
    public Block? CrackedAt(Vector3 motion) =>
        _player.TestMove(_player.GlobalTransform, motion, _moveHit) && _moveHit.GetCollider() is Block { Cracked: true, Broken: false } block ? block : null;

    /// <summary>With the Drill, breaks the cracked rock the body would run into moving by <paramref name="motion"/>.</summary>
    public void Bore(Vector3 motion)
    {
        if (!_player.Abilities.Has(Ability.Drill) || motion.LengthSquared() < 1e-6f)
        {
            return;
        }

        // A few in a row: breaking one may show another right behind it.
        for (int i = 0; i < 3 && CrackedAt(motion) is { } block; i++)
        {
            block.Break(motion.Normalized());
        }
    }
}
