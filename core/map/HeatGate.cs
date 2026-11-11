using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A heat column held to being a gate (`docs/gameplay.md#heat`), pure: it rises from rock (or an exit) under its whole width to
/// a ceiling, hood or exit over its whole width, and the room leaves no way round it: no space a pilgrim's body fits through
/// links its two sides without crossing it. Read by the room check on every heat column's play-plane region; gravity is ignored,
/// so a way round counts even if only a climb could take it (a gate seals by its shape, not by the pilgrim's limits).
/// </summary>
public static class HeatGate
{
    /// <summary>How far past the column's top and foot the ceiling and the floor are looked for, meters.</summary>
    private const float Probe = 0.05f;

    /// <summary>The grid the way round is looked for on, meters: a passage narrower than the body plus this may be missed.</summary>
    private const float Cell = 0.125f;

    /// <summary>
    /// What keeps <paramref name="column"/> from being a gate, as sentences: <paramref name="solids"/> and the exits'
    /// <paramref name="openings"/> in room positions, the room's <paramref name="bounds"/>, and the pilgrim's <paramref name="body"/>
    /// (width, height).
    /// </summary>
    public static IEnumerable<string> Problems(Box column, IReadOnlyList<Box> solids, IReadOnlyList<Box> openings, Box bounds, Vector2 body)
    {
        var ends = solids.Concat(openings).ToList();
        foreach ((float from, float to) in Across(ends, column.MaxY + Probe, column))
        {
            yield return $"it stops short of what is above it from x {from:0.##} to {to:0.##}: heat rises to the roof, hood or exit over its whole width";
        }

        foreach ((float from, float to) in Across(ends, column.MinY - Probe, column))
        {
            yield return $"its foot is open from x {from:0.##} to {to:0.##}: heat rises from its source on rock";
        }

        if (WayRound(column, solids, bounds, body))
        {
            yield return "the room leaves a way round it, from its west side to its east side: a heat gate is crossed only through it";
        }
    }

    /// <summary>The stretches of the column's width that no box covers at height <paramref name="y"/>.</summary>
    private static IEnumerable<(float From, float To)> Across(IReadOnlyList<Box> boxes, float y, Box column) =>
        SlagBasin.Gaps(boxes.Where(b => b.MinY <= y && y <= b.MaxY).Select(b => (b.MinX, b.MaxX)), column.MinX, column.MaxX);

    /// <summary>
    /// Whether a body, kept off the solids and the column, can go from beside the column's west side to beside its east side
    /// (within its height) inside the bounds: a flood fill over the places the body's center may stand.
    /// </summary>
    private static bool WayRound(Box column, IReadOnlyList<Box> solids, Box bounds, Vector2 body)
    {
        int nx = (int)MathF.Ceiling((bounds.MaxX - bounds.MinX) / Cell);
        int ny = (int)MathF.Ceiling((bounds.MaxY - bounds.MinY) / Cell);
        if (nx <= 0 || ny <= 0)
        {
            return false;
        }

        var blocked = new bool[nx, ny];
        foreach (Box solid in solids.Append(column))
        {
            // Where the body's center can't be: the box grown by half the body (touching along an edge is free).
            Box keep = new(solid.MinX - 0.5f * body.X, solid.MinY - 0.5f * body.Y, solid.MaxX + 0.5f * body.X, solid.MaxY + 0.5f * body.Y);
            for (int i = Index(keep.MinX, bounds.MinX, nx); i <= Index(keep.MaxX, bounds.MinX, nx); i++)
            {
                for (int j = Index(keep.MinY, bounds.MinY, ny); j <= Index(keep.MaxY, bounds.MinY, ny); j++)
                {
                    float x = X(i, bounds), y = Y(j, bounds);
                    if (keep.MinX < x && x < keep.MaxX && keep.MinY < y && y < keep.MaxY)
                    {
                        blocked[i, j] = true;
                    }
                }
            }
        }

        // Beside each side: the first free places off it, along the column's height (the body sharing some of it).
        float west = column.MinX - 0.5f * body.X, east = column.MaxX + 0.5f * body.X;
        float low = column.MinY - 0.5f * body.Y, high = column.MaxY + 0.5f * body.Y;
        bool Beside(int i, int j, bool westSide) =>
            !blocked[i, j] && low < Y(j, bounds) && Y(j, bounds) < high
            && (westSide ? X(i, bounds) <= west && X(i, bounds) > west - Cell : X(i, bounds) >= east && X(i, bounds) < east + Cell);

        var seen = new bool[nx, ny];
        var queue = new Queue<(int I, int J)>();
        for (int i = 0; i < nx; i++)
        {
            for (int j = 0; j < ny; j++)
            {
                if (Beside(i, j, westSide: true))
                {
                    seen[i, j] = true;
                    queue.Enqueue((i, j));
                }
            }
        }

        while (queue.Count > 0)
        {
            (int i, int j) = queue.Dequeue();
            if (Beside(i, j, westSide: false))
            {
                return true;
            }

            foreach ((int di, int dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int a = i + di, b = j + dj;
                if (a >= 0 && a < nx && b >= 0 && b < ny && !seen[a, b] && !blocked[a, b])
                {
                    seen[a, b] = true;
                    queue.Enqueue((a, b));
                }
            }
        }

        return false;
    }

    private static int Index(float v, float min, int n) => Math.Clamp((int)MathF.Floor((v - min) / Cell), 0, n - 1);

    private static float X(int i, Box bounds) => bounds.MinX + (i + 0.5f) * Cell;

    private static float Y(int j, Box bounds) => bounds.MinY + (j + 0.5f) * Cell;
}
