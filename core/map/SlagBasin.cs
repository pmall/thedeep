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

    /// <summary>The least a solid's top may lie under or over the slag's bed and surface to count as sunken ground, meters.</summary>
    private const float Tolerance = 0.01f;

    /// <summary>What the basin of <paramref name="slag"/> lacks among <paramref name="solids"/> (room positions), as sentences.</summary>
    public static IEnumerable<string> Problems(Box slag, IReadOnlyList<Box> solids)
    {
        float bedY = slag.MinY - Probe;
        foreach ((float from, float to) in Spans.Gaps(solids.Where(s => s.MinY <= bedY && bedY <= s.MaxY).Select(s => (s.MinX, s.MaxX)), slag.MinX, slag.MaxX))
        {
            yield return $"its bed at y {Fmt.Num(slag.MinY)} is open from x {Fmt.Num(from)} to {Fmt.Num(to)}: solid lies under the whole width of slag";
        }

        foreach ((string side, float x) in new[] { ("west", slag.MinX - Probe), ("east", slag.MaxX + Probe) })
        {
            foreach ((float from, float to) in Spans.Gaps(solids.Where(s => s.MinX <= x && x <= s.MaxX).Select(s => (s.MinY, s.MaxY)), slag.MinY, slag.MaxY))
            {
                yield return $"its {side} bank is open from y {Fmt.Num(from)} to {Fmt.Num(to)}: solid stands beside slag from its bed to its surface";
            }
        }
    }

    /// <summary>
    /// Ground among <paramref name="solids"/> a body <paramref name="halfWidth"/> wide each side of its feet stands on in
    /// <paramref name="slag"/>, as sentences: an open top face lying between the bed and the surface, within a half width of the
    /// slag's span (a pillar sunk under the surface, a bank's step down beside it). Standing there would burn on rock, and a set-back
    /// never lands there (`docs/gameplay.md#slag`); a top flush with the surface, or under a solid, is no such ground.
    /// </summary>
    public static IEnumerable<string> SunkenGround(Box slag, IReadOnlyList<Box> solids, float halfWidth)
    {
        float west = slag.MinX - halfWidth, east = slag.MaxX + halfWidth;
        foreach (Box s in solids.Where(s => s.MaxY > slag.MinY + Tolerance && s.MaxY < slag.MaxY - Tolerance && s.MinX < east && s.MaxX > west))
        {
            float over = s.MaxY + Probe;
            IEnumerable<(float, float)> roofs = solids.Where(o => o.MinY <= over && over <= o.MaxY).Select(o => (o.MinX, o.MaxX));
            foreach ((float from, float to) in Spans.Gaps(roofs, Math.Max(s.MinX, west), Math.Min(s.MaxX, east)))
            {
                yield return $"ground at y {Fmt.Num(s.MaxY)} from x {Fmt.Num(from)} to {Fmt.Num(to)} lies under its surface at y {Fmt.Num(slag.MaxY)}: " +
                             "a body standing there is in the slag";
            }
        }
    }
}
