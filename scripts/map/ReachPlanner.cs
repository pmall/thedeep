using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>The base moveset's numbers (copied from <c>player_tuning.tres</c>, the single source), as the planner simulates them.</summary>
public sealed record MoveLimits(
    float RunSpeed, float AirAcceleration, float AirDeceleration, float JumpHeight, float JumpTimeToApex, float JumpCutFactor,
    float FallGravityMultiplier, float ApexHangThreshold, float ApexGravityMultiplier, float MaxFallSpeed,
    float DashDistance, float DashDuration, float DashExitSpeedFactor, int AirDashes,
    float Width = 0.7f, float Height = 1.6f)
{
    public float Gravity => JumpMath.Gravity(JumpHeight, JumpTimeToApex);
    public float TakeOff => JumpMath.TakeOffSpeed(JumpHeight, JumpTimeToApex);
}

/// <summary>An axis-aligned solid on the play plane.</summary>
public readonly record struct Box(float MinX, float MinY, float MaxX, float MaxY)
{
    public bool Overlaps(Box o) => MinX < o.MaxX && o.MinX < MaxX && MinY < o.MaxY && o.MinY < MaxY;

    public static Box Around(Vector2 center, Vector2 size) =>
        new(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f, center.X + size.X * 0.5f, center.Y + size.Y * 0.5f);
}

/// <summary>A floor the pilgrim can stand on: the top of a solid at <see cref="Y"/>, feet from <see cref="MinX"/> to <see cref="MaxX"/>.</summary>
public readonly record struct Ledge(float Y, float MinX, float MaxX);

/// <summary>
/// What the base moveset can reach in one room, pure (M7's geometry sanity check, docs/long-term.md): the
/// pilgrim's body as a box walks along ledges and is simulated tick by tick through jumps (full and cut),
/// walk-offs and air dashes, with the real tuning numbers and collisions against the room's solids. From
/// an arrival point it finds every ledge it can stand on and every trigger area (an exit) its body can
/// enter. Conservative where it must be: the body is a box (the real capsule slips off edges a little
/// more easily), take-offs are sampled (ledge ends and every 2 m), so a gap the planner can't cross may
/// still be crossable by a hair; the check treats "reached" as proof, "not reached" as the designer's claim.
/// </summary>
public sealed class ReachPlanner
{
    private const float Dt = 1f / 60f;
    private const int MaxTicks = 240;
    private const float Skin = 0.001f;

    private readonly MoveLimits _m;
    private readonly List<Box> _solids;
    private readonly List<Ledge> _ledges;

    public ReachPlanner(MoveLimits limits, IEnumerable<Box> solids)
    {
        _m = limits;
        _solids = solids.ToList();
        _ledges = FindLedges();
    }

    public IReadOnlyList<Ledge> Ledges => _ledges;

    /// <summary>
    /// The trigger areas (by index into <paramref name="triggers"/>) the base moveset can enter starting at
    /// <paramref name="arrival"/> (feet) with <paramref name="velocity"/>, and the ledges it can stand on.
    /// </summary>
    public (HashSet<int> Triggers, HashSet<int> Ledges) Reach(Vector2 arrival, Vector2 velocity, IReadOnlyList<Box> triggers, int steer = 0)
    {
        var reachedTriggers = new HashSet<int>();
        var reachedLedges = new HashSet<int>();
        var queue = new Queue<int>();

        void Land(int ledge)
        {
            if (ledge >= 0 && reachedLedges.Add(ledge))
            {
                queue.Enqueue(ledge);
                Ledge l = _ledges[ledge];
                // Standing anywhere along it.
                var walk = new Box(l.MinX - _m.Width * 0.5f, l.Y + Skin, l.MaxX + _m.Width * 0.5f, l.Y + _m.Height);
                for (int t = 0; t < triggers.Count; t++)
                {
                    if (walk.Overlaps(triggers[t]))
                    {
                        reachedTriggers.Add(t);
                    }
                }
            }
        }

        // The arrival itself: falling (or rising) from the marker, with and without steering.
        foreach (int dir in steer != 0 ? new[] { steer } : new[] { -1, 0, 1 })
        {
            Simulate(arrival, velocity, dir, jumpTicks: 0, dashTick: -1, triggers, reachedTriggers, Land);
        }

        while (queue.Count > 0)
        {
            Ledge l = _ledges[queue.Dequeue()];
            foreach (float x in TakeOffs(l))
            {
                var at = new Vector2(x, l.Y);
                foreach (int dir in new[] { -1, 0, 1 })
                {
                    var running = new Vector2(dir * _m.RunSpeed, 0f);
                    foreach (int jump in new[] { -1, 6, 12 })
                    {
                        foreach (int dash in new[] { -1, 6, 14, 22, 30 })
                        {
                            Simulate(at, running, dir, jump, dash, triggers, reachedTriggers, Land);
                        }
                    }

                    // Walking off the end instead of jumping (the sim starts with vy = 0 and gravity takes over past the edge).
                    if (dir != 0)
                    {
                        foreach (int dash in new[] { -1, 4, 12, 20 })
                        {
                            Simulate(at, running, dir, jumpTicks: 0, dash, triggers, reachedTriggers, Land);
                        }
                    }
                }
            }
        }

        return (reachedTriggers, reachedLedges);
    }

    /// <summary>Where on a ledge to take off: its ends, and every 2 m between.</summary>
    private IEnumerable<float> TakeOffs(Ledge l)
    {
        float a = l.MinX, b = l.MaxX;
        yield return a;
        if (b - a > 0.01f)
        {
            yield return b;
        }

        for (float x = a + 2f; x < b - 1f; x += 2f)
        {
            yield return x;
        }
    }

    /// <summary>
    /// One trajectory: from <paramref name="start"/> (feet) holding <paramref name="dir"/>; jump on the first tick held for
    /// <paramref name="jumpTicks"/> (−1: held to the apex, 0: no jump); an air dash toward <paramref name="dir"/> at
    /// <paramref name="dashTick"/> (−1: none). Ends on landing (reported) or after a time limit.
    /// </summary>
    private void Simulate(Vector2 start, Vector2 velocity, int dir, int jumpTicks, int dashTick, IReadOnlyList<Box> triggers,
        HashSet<int> reachedTriggers, Action<int> land)
    {
        Vector2 p = start, v = velocity;
        bool jumping = jumpTicks != 0, cut = false, airborne = jumping;
        if (jumping)
        {
            v.Y = _m.TakeOff;
        }

        int dashLeft = 0, dashDir = dir == 0 ? 1 : dir;
        bool dashed = false;
        for (int tick = 0; tick < MaxTicks; tick++)
        {
            bool held = jumpTicks < 0 || tick < jumpTicks;
            if (dashTick >= 0 && tick == dashTick && !dashed && _m.AirDashes > 0 && airborne)
            {
                dashed = true;
                dashLeft = (int)MathF.Round(_m.DashDuration / Dt);
            }

            if (dashLeft > 0)
            {
                v = new Vector2(dashDir * _m.DashDistance / _m.DashDuration, 0f);
                if (--dashLeft == 0)
                {
                    v = new Vector2(dashDir * _m.RunSpeed * _m.DashExitSpeedFactor, 0f);
                }
            }
            else
            {
                if (jumping && !cut && !held && v.Y > 0f)
                {
                    v.Y *= _m.JumpCutFactor;
                    cut = true;
                }

                float target = dir * _m.RunSpeed;
                bool accelerating = target != 0f && (v.X == 0f || MathF.Sign(target) == MathF.Sign(v.X)) && MathF.Abs(v.X) <= _m.RunSpeed;
                v.X = Motion.Approach(v.X, target, (accelerating ? _m.AirAcceleration : _m.AirDeceleration) * Dt);
                float g = _m.Gravity;
                if (held && jumping && MathF.Abs(v.Y) < _m.ApexHangThreshold)
                {
                    g *= _m.ApexGravityMultiplier;
                }
                else if (v.Y < 0f)
                {
                    g *= _m.FallGravityMultiplier;
                }

                v.Y = MathF.Max(v.Y - g * Dt, -_m.MaxFallSpeed);
            }

            // Move X, then Y, stopping at faces.
            p.X = MoveAxis(p, v.X * Dt, horizontal: true, out bool hitX);
            if (hitX)
            {
                v.X = 0f;
            }

            float before = p.Y;
            p.Y = MoveAxis(p, v.Y * Dt, horizontal: false, out bool hitY);
            if (MathF.Abs(p.Y - before) > 1e-5f)
            {
                airborne = true;
            }

            Touch(p, triggers, reachedTriggers);
            if (hitY && v.Y < 0f)
            {
                // On a floor: a landing after a flight ends the trajectory; before take-off (walking to an edge) it goes on.
                land(LedgeAt(p));
                if (airborne || dir == 0 || hitX)
                {
                    return;
                }

                v.Y = 0f;
            }
            else if (hitY)
            {
                v.Y = 0f;
            }
        }
    }

    private void Touch(Vector2 p, IReadOnlyList<Box> triggers, HashSet<int> reached)
    {
        Box body = BodyAt(p);
        for (int t = 0; t < triggers.Count; t++)
        {
            if (body.Overlaps(triggers[t]))
            {
                reached.Add(t);
            }
        }
    }

    private Box BodyAt(Vector2 feet) => new(feet.X - _m.Width * 0.5f, feet.Y, feet.X + _m.Width * 0.5f, feet.Y + _m.Height);

    /// <summary>Moves the body along one axis by <paramref name="delta"/>, stopping against the first solid.</summary>
    private float MoveAxis(Vector2 p, float delta, bool horizontal, out bool hit)
    {
        hit = false;
        Box body = BodyAt(p);
        float moved = delta;
        foreach (Box s in _solids)
        {
            if (horizontal)
            {
                if (body.MinY >= s.MaxY - Skin || body.MaxY <= s.MinY + Skin)
                {
                    continue;
                }

                if (delta > 0f && body.MaxX <= s.MinX + Skin && body.MaxX + moved > s.MinX)
                {
                    moved = s.MinX - body.MaxX - Skin;
                    hit = true;
                }
                else if (delta < 0f && body.MinX >= s.MaxX - Skin && body.MinX + moved < s.MaxX)
                {
                    moved = s.MaxX - body.MinX + Skin;
                    hit = true;
                }
            }
            else
            {
                if (body.MinX >= s.MaxX - Skin || body.MaxX <= s.MinX + Skin)
                {
                    continue;
                }

                if (delta > 0f && body.MaxY <= s.MinY + Skin && body.MaxY + moved > s.MinY)
                {
                    moved = s.MinY - body.MaxY - Skin;
                    hit = true;
                }
                else if (delta < 0f && body.MinY >= s.MaxY - Skin && body.MinY + moved < s.MaxY)
                {
                    moved = s.MaxY - body.MinY + Skin;
                    hit = true;
                }
            }
        }

        return (horizontal ? p.X : p.Y) + moved;
    }

    /// <summary>The ledge whose standing range holds feet at <paramref name="p"/>, or −1.</summary>
    private int LedgeAt(Vector2 p)
    {
        for (int i = 0; i < _ledges.Count; i++)
        {
            Ledge l = _ledges[i];
            if (MathF.Abs(l.Y - p.Y) < 0.01f && p.X >= l.MinX - 0.4f && p.X <= l.MaxX + 0.4f)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The standing ranges on every solid's top: where the feet can be with the body clear of other solids
    /// (split where a solid sits on top or overhangs within a body's height).
    /// </summary>
    private List<Ledge> FindLedges()
    {
        var ledges = new List<Ledge>();
        float half = _m.Width * 0.5f;
        foreach (Box s in _solids)
        {
            var free = new List<(float A, float B)> { (s.MinX, s.MaxX) };
            var room = new Box(s.MinX - half, s.MaxY + Skin, s.MaxX + half, s.MaxY + _m.Height);
            foreach (Box o in _solids)
            {
                if (!o.Overlaps(room))
                {
                    continue;
                }

                // Feet can't be where the body would overlap o.
                float cutA = o.MinX - half, cutB = o.MaxX + half;
                free = free.SelectMany(f => Cut(f, cutA, cutB)).ToList();
            }

            foreach ((float a, float b) in free)
            {
                if (b - a > 0.05f)
                {
                    ledges.Add(new Ledge(s.MaxY, a, b));
                }
            }
        }

        return ledges;
    }

    private static IEnumerable<(float, float)> Cut((float A, float B) f, float a, float b)
    {
        if (b <= f.A || a >= f.B)
        {
            yield return f;
            yield break;
        }

        if (a > f.A)
        {
            yield return (f.A, a);
        }

        if (b < f.B)
        {
            yield return (b, f.B);
        }
    }
}
