using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// A pocket: a stretch of ground from <see cref="MinX"/> to <see cref="MaxX"/> (the capsule's center) on solid <see cref="Ground"/>'s
/// top at <see cref="Y"/> (and any ground flush with it), every place of which is under a solid lower than a full jump's reach, so no
/// full jump can be made from it; <see cref="Ceiling"/> is the lowest unmarked solid over it, <see cref="Clearance"/> meters over the ground.
/// </summary>
public readonly record struct LowCeiling(int Ground, int Ceiling, float Y, float MinX, float MaxX, float Clearance);

/// <summary>The kinds of meeting between two solids that the physics or the renderer gets wrong.</summary>
public enum SeamKind
{
    /// <summary>Two tops side by side, one higher by less than the ledge assist (and more than nothing): a bump the run trips on.</summary>
    Step,

    /// <summary>Two solids overlapping with a face in the same plane: the faces fight for the same depth (z-fighting).</summary>
    Coplanar,
}

/// <summary>A meeting of solids <see cref="A"/> and <see cref="B"/> at (<see cref="X"/>, <see cref="Y"/>): a step's height, or a shared face's length.</summary>
public readonly record struct Seam(SeamKind Kind, int A, int B, float X, float Y, float Size);

/// <summary>
/// Rules on a room's solids (play-plane boxes) that hold the ground it is walked on, pure, each catching a defect measured in play:
/// pockets under low ceilings (<see cref="Headroom"/>), steps that stop a run and faces in one plane (<see cref="Seams"/>), a seam
/// frame's column entered by another solid (<see cref="Intrusions"/>), and a mesh lying across the play plane where nothing solid is
/// (<see cref="OpenSpans"/>). The room check fails on steps, intrusions and open meshes, and counts pockets and coplanar faces.
/// Tops meeting flush side by side are no rule: the capsule never lost a floor over such a seam in play.
/// </summary>
public static class GroundRules
{
    /// <summary>How close two planes are to count as one, meters.</summary>
    private const float Eps = 0.01f;

    /// <summary>The shortest stretch that counts, meters (narrower, nobody stands or passes there).</summary>
    private const float Least = 0.2f;

    /// <summary>
    /// Every pocket among <paramref name="solids"/>: a stretch of ground (flush tops joined) whose every place lies under a solid lower
    /// than <paramref name="reach"/> (a jump's head over its feet; the capsule's <paramref name="radius"/> widens what its head meets),
    /// under at least one of which the pilgrim stands upright (<paramref name="height"/>). A jump from there always meets a solid before
    /// its apex. Ground with a free place to step out to before jumping is no pocket: a ceiling over part of a floor is the room's shape,
    /// not a defect. Solids in <paramref name="low"/> are low on purpose (a seam frame's, low by construction, or one a room marks):
    /// they cover the ground but are never the finding. Ground outside <paramref name="bounds"/> (the room's) is left out.
    /// </summary>
    public static IEnumerable<LowCeiling> Headroom(IReadOnlyList<Box> solids, IReadOnlySet<int> low, float reach, float height, float radius, Box bounds)
    {
        foreach ((int g, float y, float from, float to) in Walkways(solids, bounds))
        {
            var over = Enumerable.Range(0, solids.Count)
                .Where(k => solids[k].MinY > y + Eps && solids[k].MinY < y + reach - Eps && solids[k].MinX - radius < to && from < solids[k].MaxX + radius).ToList();
            if (SlagBasin.Gaps(over.Select(k => (solids[k].MinX - radius, solids[k].MaxX + radius)), from, to).Any(s => s.To - s.From >= Least))
            {
                continue; // a free place to jump from
            }

            int[] ceilings = over.Where(k => !low.Contains(k) && solids[k].MinY - y >= height - Eps).OrderBy(k => solids[k].MinY).ToArray();
            if (ceilings.Length > 0)
            {
                yield return new LowCeiling(g, ceilings[0], y, from, to, solids[ceilings[0]].MinY - y);
            }
        }
    }

    /// <summary>
    /// Every seam among <paramref name="solids"/>: tops meeting side by side on open ground a step lower than <paramref name="assist"/>
    /// (the ledge assist's lift, which works only in the air), unless the low side lies in <paramref name="unwalked"/> (water swum
    /// through, a machine's body at rest filling the dip), and overlapping solids with a face in one plane.
    /// </summary>
    public static IEnumerable<Seam> Seams(IReadOnlyList<Box> solids, float assist, IReadOnlyList<Box> unwalked)
    {
        for (int i = 0; i < solids.Count; i++)
        {
            for (int j = i + 1; j < solids.Count; j++)
            {
                Box a = solids[i], b = solids[j];
                foreach ((Box left, Box right, int l, int r) in new[] { (a, b, i, j), (b, a, j, i) })
                {
                    if (Math.Abs(left.MaxX - right.MinX) > Eps)
                    {
                        continue;
                    }

                    float x = left.MaxX, step = Math.Abs(left.MaxY - right.MaxY), lowY = Math.Min(left.MaxY, right.MaxY);
                    float side = left.MaxY <= right.MaxY ? x - Eps : x + Eps;
                    if (step > Eps && step < assist && !Inside(solids, side, lowY + Eps) && !Inside(unwalked, side, lowY + Eps))
                    {
                        yield return new Seam(SeamKind.Step, l, r, x, lowY, step);
                    }
                }

                foreach (Seam face in SharedFaces(a, b, i, j))
                {
                    yield return face;
                }
            }
        }
    }

    /// <summary>
    /// Every pair of solids, one built by a seam frame, that overlap: <paramref name="frame"/> gives each solid's frame (any number) or
    /// null for the room's own. A frame owns its column (`docs/gameplay.md#seam-rule`): another solid reaching in draws a joint line
    /// across the wall and puts faces in one plane. The overlap's box is returned.
    /// </summary>
    public static IEnumerable<(int A, int B, Box Overlap)> Intrusions(IReadOnlyList<Box> solids, IReadOnlyList<int?> frame)
    {
        for (int i = 0; i < solids.Count; i++)
        {
            for (int j = i + 1; j < solids.Count; j++)
            {
                Box a = solids[i], b = solids[j];
                var o = new Box(Math.Max(a.MinX, b.MinX), Math.Max(a.MinY, b.MinY), Math.Min(a.MaxX, b.MaxX), Math.Min(a.MaxY, b.MaxY));
                if (frame[i] != frame[j] && (frame[i] is not null || frame[j] is not null) && o.MaxX - o.MinX > Eps && o.MaxY - o.MinY > Eps)
                {
                    yield return (i, j, o);
                }
            }
        }
    }

    /// <summary>
    /// The stretches of a mesh lying across the play plane (its play-plane box <paramref name="mesh"/>) where no solid stands at its
    /// middle height: the pilgrim passes through it there. Stretches narrower than <see cref="Least"/> are left out.
    /// </summary>
    public static IEnumerable<(float From, float To)> OpenSpans(Box mesh, IReadOnlyList<Box> solids)
    {
        float y = 0.5f * (mesh.MinY + mesh.MaxY);
        return SlagBasin.Gaps(solids.Where(s => s.MinY <= y && y <= s.MaxY).Select(s => (s.MinX, s.MaxX)), mesh.MinX, mesh.MaxX)
            .Where(s => s.To - s.From >= Least);
    }

    /// <summary>
    /// The walkways inside <paramref name="bounds"/>: the open stretches of solid tops (no other solid buries them), those of tops flush
    /// with each other and touching joined into one; each with the first solid it runs on and its height.
    /// </summary>
    private static IEnumerable<(int Ground, float Y, float From, float To)> Walkways(IReadOnlyList<Box> solids, Box bounds)
    {
        var stretches = new List<(int G, float Y, float From, float To)>();
        for (int g = 0; g < solids.Count; g++)
        {
            float y = solids[g].MaxY;
            if (y >= bounds.MinY && y <= bounds.MaxY)
            {
                stretches.AddRange(Open(solids, g, y).Select(s => (g, y, Math.Max(s.From, bounds.MinX), Math.Min(s.To, bounds.MaxX))).Where(s => s.Item4 - s.Item3 >= Least));
            }
        }

        stretches.Sort((p, q) => p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.From.CompareTo(q.From));
        for (int i = 0; i < stretches.Count;)
        {
            (int g, float y, float from, float to) = stretches[i];
            int k = i + 1;
            for (; k < stretches.Count && Math.Abs(stretches[k].Y - y) <= Eps && stretches[k].From <= to + Eps; k++)
            {
                to = Math.Max(to, stretches[k].To);
            }

            yield return (g, y, from, to);
            i = k;
        }
    }

    /// <summary>The stretches of solid <paramref name="g"/>'s top at <paramref name="y"/> that no other solid buries.</summary>
    private static IEnumerable<(float From, float To)> Open(IReadOnlyList<Box> solids, int g, float y) =>
        SlagBasin.Gaps(solids.Where((s, k) => k != g && s.MinY < y + Eps && y + Eps < s.MaxY).Select(s => (s.MinX, s.MaxX)), solids[g].MinX, solids[g].MaxX)
            .Where(s => s.To - s.From >= Least);

    private static bool Inside(IReadOnlyList<Box> boxes, float x, float y) => boxes.Any(s => s.MinX < x && x < s.MaxX && s.MinY < y && y < s.MaxY);

    /// <summary>The faces of overlapping <paramref name="a"/> and <paramref name="b"/> that lie in one plane, facing the same way.</summary>
    private static IEnumerable<Seam> SharedFaces(Box a, Box b, int i, int j)
    {
        float alongX = Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX), alongY = Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY);
        if (alongX <= Eps || alongY <= Eps)
        {
            yield break; // apart, or touching along an edge: faces back to back hide each other
        }

        float midX = Math.Max(a.MinX, b.MinX) + 0.5f * alongX, midY = Math.Max(a.MinY, b.MinY) + 0.5f * alongY;
        if (Math.Abs(a.MaxY - b.MaxY) <= Eps)
        {
            yield return new Seam(SeamKind.Coplanar, i, j, midX, a.MaxY, alongX);
        }

        if (Math.Abs(a.MinY - b.MinY) <= Eps)
        {
            yield return new Seam(SeamKind.Coplanar, i, j, midX, a.MinY, alongX);
        }

        if (Math.Abs(a.MinX - b.MinX) <= Eps)
        {
            yield return new Seam(SeamKind.Coplanar, i, j, a.MinX, midY, alongY);
        }

        if (Math.Abs(a.MaxX - b.MaxX) <= Eps)
        {
            yield return new Seam(SeamKind.Coplanar, i, j, a.MaxX, midY, alongY);
        }
    }
}
