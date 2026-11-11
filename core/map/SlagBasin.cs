using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// A slag body held to its basin (`docs/gameplay.md#slag`), pure: slag fills a basin from its surface down to a bed, between banks,
/// so solid rock lies under its whole width (the bed, pillars rising from it included) and beside it on both sides from the bed up
/// to the surface (the banks). Read by the room check on every slag body's play-plane region.
/// </summary>
public static class SlagBasin
{
    /// <summary>How far past the slag's edge the bed and banks are looked for, meters (solids meet it there, never overlap it).</summary>
    private const float Probe = 0.05f;

    /// <summary>The shortest stretch of a bed or bank left open that counts as a gap, meters.</summary>
    private const float Tolerance = 0.01f;

    /// <summary>What the basin of <paramref name="slag"/> lacks among <paramref name="solids"/> (room positions), as sentences.</summary>
    public static IEnumerable<string> Problems(Box slag, IReadOnlyList<Box> solids)
    {
        float bedY = slag.MinY - Probe;
        foreach ((float from, float to) in Gaps(solids.Where(s => s.MinY <= bedY && bedY <= s.MaxY).Select(s => (s.MinX, s.MaxX)), slag.MinX, slag.MaxX))
        {
            yield return $"its bed at y {slag.MinY:0.##} is open from x {from:0.##} to {to:0.##}: solid lies under the whole width of slag";
        }

        foreach ((string side, float x) in new[] { ("west", slag.MinX - Probe), ("east", slag.MaxX + Probe) })
        {
            foreach ((float from, float to) in Gaps(solids.Where(s => s.MinX <= x && x <= s.MaxX).Select(s => (s.MinY, s.MaxY)), slag.MinY, slag.MaxY))
            {
                yield return $"its {side} bank is open from y {from:0.##} to {to:0.##}: solid stands beside slag from its bed to its surface";
            }
        }
    }

    /// <summary>The stretches of [<paramref name="from"/>, <paramref name="to"/>] no span covers.</summary>
    private static IEnumerable<(float From, float To)> Gaps(IEnumerable<(float Min, float Max)> spans, float from, float to)
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
