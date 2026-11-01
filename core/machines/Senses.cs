using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The senses of the behavior vocabulary (docs/long-term.md): seeing the player, feeling them near, hearing
/// an impact, noticing nearby machines. Pure functions of a <see cref="Perception"/>, so brains combine them
/// freely and tests can call them directly.
/// </summary>
public static class Senses
{
    /// <summary>
    /// The player is in line of sight, within <paramref name="range"/>, and in front of the machine (or at
    /// most <paramref name="behind"/> meters behind it), within a vertical band of <paramref name="range"/> × 0.6.
    /// </summary>
    public static bool SeesPlayer(in Perception p, float range, float behind = 0f)
    {
        if (p.Player is not { InSight: true } player)
        {
            return false;
        }

        Vector2 d = player.Position - p.Position;
        float ahead = d.X * p.Facing;
        return ahead >= -behind && d.Length() <= range && MathF.Abs(d.Y) <= range * 0.6f;
    }

    /// <summary>The player is within <paramref name="radius"/>, seen or not (presence, vibration, scent of ozone).</summary>
    public static bool PlayerWithin(in Perception p, float radius) =>
        p.Player is { } player && Vector2.Distance(player.Position, p.Position) <= radius;

    /// <summary>Distance to the player, or +∞ without one.</summary>
    public static float PlayerDistance(in Perception p) =>
        p.Player is { } player ? Vector2.Distance(player.Position, p.Position) : float.PositiveInfinity;

    /// <summary>
    /// The loudest impact heard this tick, or null. An impact fades linearly to nothing at <paramref name="range"/>;
    /// it is heard if what is left reaches <paramref name="threshold"/> (m/s of impact speed).
    /// </summary>
    public static Impact? Hears(in Perception p, float range, float threshold)
    {
        Impact? loudest = null;
        float best = threshold;
        foreach (Impact impact in p.Impacts)
        {
            float heard = impact.Strength * (1f - Vector2.Distance(impact.Position, p.Position) / MathF.Max(range, 1e-3f));
            if (heard >= best)
            {
                best = heard;
                loudest = impact;
            }
        }

        return loudest;
    }

    /// <summary>How many machines of <paramref name="kind"/> (any kind if null) are within <paramref name="radius"/>.</summary>
    public static int NeighborsWithin(in Perception p, float radius, string? kind = null)
    {
        int count = 0;
        foreach (Neighbor n in p.Neighbors)
        {
            if ((kind is null || n.Kind == kind) && Vector2.Distance(n.Position, p.Position) <= radius)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Signed distance of <paramref name="point"/> ahead of the machine (negative behind it).</summary>
    public static float Ahead(in Perception p, Vector2 point) => (point.X - p.Position.X) * p.Facing;
}
