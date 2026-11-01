using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A crawler's way along its surface (<see cref="SurfacePath"/>), pure: where it is along the path, how fast it goes, and each tick's
/// step toward the part of the wanted velocity that runs along the surface.
/// </summary>
public sealed class CrawlMotion
{
    private float _speed;

    public CrawlMotion(SurfacePath path, Vector2 spawn)
    {
        Path = path;
        Reset(spawn);
    }

    public SurfacePath Path { get; }

    /// <summary>The distance along the path, meters.</summary>
    public float S { get; private set; }

    /// <summary>The point on the surface and its outward normal.</summary>
    public (Vector2 Point, Vector2 Normal) At => Path.At(S);

    /// <summary>The direction +S runs at the current point.</summary>
    public Vector2 Tangent => Path.Tangent(S);

    /// <summary>At rest at the surface point nearest <paramref name="spawn"/>.</summary>
    public void Reset(Vector2 spawn)
    {
        S = Path.Closest(spawn);
        _speed = 0f;
    }

    /// <summary>
    /// One tick: toward <paramref name="wanted"/>'s part along the surface, at most <paramref name="maxSpeed"/>, changing speed at
    /// <paramref name="acceleration"/>; stops at an open path's ends. Returns the distance moved along the path (signed).
    /// </summary>
    public float Step(Vector2 wanted, float maxSpeed, float acceleration, float dt)
    {
        float along = Math.Clamp(Vector2.Dot(wanted, Tangent), -maxSpeed, maxSpeed);
        _speed = Motion.Approach(_speed, along, acceleration * dt);
        float before = S;
        S = Path.Wrap(S + _speed * dt);
        if (Path.AtEnd(S, Math.Sign(_speed)))
        {
            _speed = 0f;
        }

        float moved = S - before;
        if (Path.Closed && MathF.Abs(moved) > Path.Length * 0.5f)
        {
            moved -= MathF.Sign(moved) * Path.Length; // wrapped round a closed path
        }

        return moved;
    }
}
