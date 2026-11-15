using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>Stretches along one axis, as the room check's hazard rules hold a bed, a bank or a roof to being unbroken.</summary>
public static class Spans
{
    /// <summary>The shortest stretch left open that counts as a gap, meters.</summary>
    public const float Tolerance = 0.01f;

    /// <summary>The stretches of [<paramref name="from"/>, <paramref name="to"/>] no span covers (wider than <see cref="Tolerance"/>).</summary>
    public static IEnumerable<(float From, float To)> Gaps(IEnumerable<(float Min, float Max)> spans, float from, float to)
    {
        float covered = from;
        foreach ((float min, float max) in spans.OrderBy(s => s.Min))
        {
            if (min > covered + Tolerance)
            {
                yield return (covered, Math.Min(min, to));
            }

            covered = Math.Max(covered, max);
            if (covered >= to)
            {
                yield break;
            }
        }

        if (covered < to - Tolerance)
        {
            yield return (covered, to);
        }
    }
}
