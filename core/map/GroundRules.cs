using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// Ground the pilgrim can stand on with a solid over it lower than a full jump's reach: standing anywhere from <see cref="MinX"/> to
/// <see cref="MaxX"/> (the capsule's center) on the top of solid <see cref="Ground"/> at <see cref="Y"/>, a jump straight up meets
/// solid <see cref="Ceiling"/>, <see cref="Clearance"/> meters over the ground.
/// </summary>
public readonly record struct LowCeiling(int Ground, int Ceiling, float Y, float MinX, float MaxX, float Clearance);

/// <summary>The kinds of meeting between two solids that the physics or the renderer gets wrong.</summary>
public enum SeamKind
{
    /// <summary>Two tops side by side, one higher by less than the ledge assist (and more than nothing): a bump the run trips on.</summary>
    Step,

    /// <summary>Two tops side by side at the same height: a ghost edge the capsule can catch on, losing the floor for a tick.</summary>
    Flush,

    /// <summary>Two solids overlapping with a face in the same plane: the faces fight for the same depth (z-fighting).</summary>
    Coplanar,
}

/// <summary>A meeting of solids <see cref="A"/> and <see cref="B"/> at (<see cref="X"/>, <see cref="Y"/>): a step's height, or a shared face's length.</summary>
public readonly record struct Seam(SeamKind Kind, int A, int B, float X, float Y, float Size);

/// <summary>
/// Rules on a room's solids (play-plane boxes) that hold the ground it is walked on (`docs/roadmap.md#volcanic-rethink`), pure:
/// headroom over ground (<see cref="Headroom"/>), seams on walking surfaces and shared faces (<see cref="Seams"/>), and a mesh
/// lying across the play plane where nothing solid is (<see cref="OpenSpans"/>). The room check reports what they find, every room of
/// both worlds; they don't fail it yet.
/// </summary>
public static class GroundRules
{
    /// <summary>How close two planes are to count as one, meters.</summary>
    private const float Eps = 0.01f;

    /// <summary>The shortest stretch that counts, meters (narrower, nobody stands or passes there).</summary>
    private const float Least = 0.2f;

    /// <summary>
    /// Every stretch of ground among <paramref name="solids"/> where the pilgrim stands upright (at least <paramref name="height"/>
    /// under the solid over it) yet a jump straight up, its head reaching <paramref name="reach"/> over its feet, meets that solid. The
    /// capsule's <paramref name="radius"/> widens what its head meets; stretches inside a <paramref name="low"/> box (a passage the room
    /// marks low, such as an exit's opening) or outside <paramref name="bounds"/> (the room's) are left out, and only the lowest solid
    /// over a stretch counts.
    /// </summary>
    public static IEnumerable<LowCeiling> Headroom(IReadOnlyList<Box> solids, float reach, float height, float radius, IReadOnlyList<Box> low, Box bounds)
    {
        for (int g = 0; g < solids.Count; g++)
        {
            Box ground = solids[g];
            float y = ground.MaxY;
            if (y < bounds.MinY || y > bounds.MaxY)
            {
                continue;
            }

            foreach ((float from, float to) in Open(solids, g, y))
            {
                for (int c = 0; c < solids.Count; c++)
                {
                    Box ceiling = solids[c];
                    float clearance = ceiling.MinY - y;
                    if (c == g || clearance < height - Eps || clearance >= reach)
                    {
                        continue;
                    }

                    float min = Math.Max(Math.Max(from, ceiling.MinX - radius), bounds.MinX), max = Math.Min(Math.Min(to, ceiling.MaxX + radius), bounds.MaxX);
                    IEnumerable<(float, float)> excused = low.Where(b => b.MinY <= y + Eps && y + height <= b.MaxY + Eps).Select(b => (b.MinX, b.MaxX))
                        .Concat(solids.Where((s, k) => k != g && k != c && s.MinY > y + Eps && s.MinY < ceiling.MinY - Eps)
                            .Select(s => (s.MinX - radius, s.MaxX + radius)));
                    foreach ((float a, float b) in SlagBasin.Gaps(excused, min, max).Where(s => s.To - s.From >= Least))
                    {
                        yield return new LowCeiling(g, c, y, a, b, clearance);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Every seam among <paramref name="solids"/>: tops meeting side by side on open ground, flush or a step lower than
    /// <paramref name="assist"/> (the ledge assist's lift), and overlapping solids with a face in one plane.
    /// </summary>
    public static IEnumerable<Seam> Seams(IReadOnlyList<Box> solids, float assist)
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
                    if (step <= Eps && !Inside(solids, x, lowY + Eps))
                    {
                        yield return new Seam(SeamKind.Flush, l, r, x, lowY, 0f);
                    }
                    else if (step > Eps && step < assist && !Inside(solids, side, lowY + Eps))
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
    /// The stretches of a mesh lying across the play plane (its play-plane box <paramref name="mesh"/>) where no solid stands at its
    /// middle height: the pilgrim passes through it there. Stretches narrower than <see cref="Least"/> are left out.
    /// </summary>
    public static IEnumerable<(float From, float To)> OpenSpans(Box mesh, IReadOnlyList<Box> solids)
    {
        float y = 0.5f * (mesh.MinY + mesh.MaxY);
        return SlagBasin.Gaps(solids.Where(s => s.MinY <= y && y <= s.MaxY).Select(s => (s.MinX, s.MaxX)), mesh.MinX, mesh.MaxX)
            .Where(s => s.To - s.From >= Least);
    }

    /// <summary>The stretches of solid <paramref name="g"/>'s top at <paramref name="y"/> that no other solid buries.</summary>
    private static IEnumerable<(float From, float To)> Open(IReadOnlyList<Box> solids, int g, float y) =>
        SlagBasin.Gaps(solids.Where((s, k) => k != g && s.MinY < y + Eps && y + Eps < s.MaxY).Select(s => (s.MinX, s.MaxX)), solids[g].MinX, solids[g].MaxX)
            .Where(s => s.To - s.From >= Least);

    private static bool Inside(IReadOnlyList<Box> solids, float x, float y) => solids.Any(s => s.MinX < x && x < s.MaxX && s.MinY < y && y < s.MaxY);

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
