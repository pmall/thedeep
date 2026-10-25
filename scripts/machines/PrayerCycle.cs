using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// A machine's prayer: a slow cycle of bowing down, holding, and rising, as a 0–1 bow amount. Praying
/// machines near each other fall into step (each phase is pulled toward its neighbors', like fireflies),
/// so a group of them bows together after a while. Pure, part of the behavior vocabulary.
/// </summary>
public sealed class PrayerCycle
{
    public PrayerCycle(float period, float phase = 0f)
    {
        Period = MathF.Max(period, 0.1f);
        Phase = Wrap(phase);
    }

    /// <summary>Seconds per bow.</summary>
    public float Period { get; }

    /// <summary>Where in the cycle, 0 to 1.</summary>
    public float Phase { get; private set; }

    /// <summary>How strongly neighbors pull the phase, cycles per second at most.</summary>
    public float Coupling { get; init; } = 0.15f;

    /// <summary>The bow amount now: 0 upright, 1 fully bowed.</summary>
    public float Bow => BowAt(Phase);

    /// <summary>Advances by <paramref name="delta"/> seconds, pulled toward the phases of <paramref name="neighbors"/>.</summary>
    public void Step(float delta, IReadOnlyList<float> neighbors)
    {
        float pull = 0f;
        foreach (float other in neighbors)
        {
            pull += MathF.Sin(MathF.Tau * (other - Phase));
        }

        if (neighbors.Count > 0)
        {
            pull /= neighbors.Count;
        }

        Phase = Wrap(Phase + delta / Period + Coupling * pull * delta);
    }

    /// <summary>The bow curve: bow down (0–0.3), hold (0.3–0.5), rise slowly (0.5–0.85), rest upright.</summary>
    public static float BowAt(float phase)
    {
        phase = Wrap(phase);
        if (phase < 0.3f)
        {
            return Smooth(phase / 0.3f);
        }

        if (phase < 0.5f)
        {
            return 1f;
        }

        return phase < 0.85f ? 1f - Smooth((phase - 0.5f) / 0.35f) : 0f;
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Wrap(float phase) => phase - MathF.Floor(phase);
}
