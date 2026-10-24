using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A hanging chain of points for secondary motion (coat tails, hair, cables, a lantern's chain), Godot-free.
/// Simulated in world space so it trails behind moves: Verlet integration with gravity and air drag, a
/// pull back toward its rest shape (held in the anchor's frame), then inextensible links solved from the
/// anchor outward (follow the leader). Point 0 is the anchor.
/// </summary>
public sealed class VerletChain
{
    private readonly Vector3[] _previous;
    private readonly Vector3[] _rest;

    public VerletChain(int links, float linkLength)
    {
        Links = Math.Max(1, links);
        LinkLength = MathF.Max(1e-3f, linkLength);
        Points = new Vector3[Links + 1];
        _previous = new Vector3[Links + 1];
        _rest = new Vector3[Links + 1];
    }

    public int Links { get; }
    public float LinkLength { get; }

    /// <summary>Current positions, anchor first. Read only outside the chain.</summary>
    public Vector3[] Points { get; }

    /// <summary>Downward acceleration, m/s².</summary>
    public float Gravity { get; set; } = 9.8f;

    /// <summary>Air drag in 1/s: how fast the chain's own motion through the air dies out. Makes it stream behind a run.</summary>
    public float Drag { get; set; } = 2f;

    /// <summary>Pull toward the rest shape in 1/s: 0 hangs freely, high values hold the shape (a stiff plate).</summary>
    public float Stiffness { get; set; } = 4f;

    /// <summary>Points may not go below this height (the floor under a standing body); NaN for none.</summary>
    public float Floor { get; set; } = float.NaN;

    /// <summary>
    /// Rest direction of the first link in the anchor's frame, and the bend (radians per link, around the
    /// anchor's Z axis) the chain curls by at rest: hair sweeping back, a tail curling up.
    /// </summary>
    public Vector3 RestDirection { get; set; } = -Vector3.UnitY;

    public float RestCurl { get; set; }

    /// <summary>Puts the chain at rest under the anchor, still (spawn, teleport).</summary>
    public void Reset(Vector3 anchor, Quaternion rotation)
    {
        RestShape(anchor, rotation);
        Array.Copy(_rest, Points, _rest.Length);
        Array.Copy(_rest, _previous, _rest.Length);
    }

    /// <summary>Advances the chain by <paramref name="delta"/> seconds with its anchor now at <paramref name="anchor"/>, turned by <paramref name="rotation"/>.</summary>
    public void Step(float delta, Vector3 anchor, Quaternion rotation)
    {
        if (delta <= 0f)
        {
            return;
        }

        RestShape(anchor, rotation);
        float keep = MathF.Exp(-Drag * delta);
        float pull = 1f - MathF.Exp(-Stiffness * delta);
        var gravity = new Vector3(0f, -Gravity * delta * delta, 0f);

        Points[0] = anchor;
        _previous[0] = anchor;
        for (int i = 1; i < Points.Length; i++)
        {
            Vector3 p = Points[i];
            Vector3 next = p + (p - _previous[i]) * keep + gravity;
            _previous[i] = p;
            Points[i] = Vector3.Lerp(next, _rest[i], pull);
        }

        for (int i = 1; i < Points.Length; i++)
        {
            Vector3 link = Points[i] - Points[i - 1];
            float length = link.Length();
            Vector3 direction = length > 1e-6f ? link / length : Vector3.Normalize(_rest[i] - _rest[i - 1]);
            Vector3 solved = Points[i - 1] + direction * LinkLength;
            if (!float.IsNaN(Floor) && solved.Y < Floor)
            {
                solved.Y = Floor;
            }

            // Follow the leader moves the point without its velocity knowing; carry the correction into the
            // previous position too, or links would gain energy and jitter.
            _previous[i] += (solved - Points[i]) * 0.9f;
            Points[i] = solved;
        }
    }

    private void RestShape(Vector3 anchor, Quaternion rotation)
    {
        _rest[0] = anchor;
        Vector3 direction = RestDirection.LengthSquared() > 1e-8f ? Vector3.Normalize(RestDirection) : -Vector3.UnitY;
        Quaternion curl = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, RestCurl);
        for (int i = 1; i < _rest.Length; i++)
        {
            _rest[i] = _rest[i - 1] + Vector3.Transform(direction, rotation) * LinkLength;
            direction = Vector3.Transform(direction, curl);
        }
    }
}
