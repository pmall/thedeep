using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// A room's solids by square cell, so a moving body is tested against the solids near it, not every solid in the room (the reach
/// planner moves a body thousands of times per walk). Fixed once built; safe to query from several threads.
/// </summary>
public sealed class SolidGrid
{
    /// <summary>A cell's side, meters: a few ticks of the fastest move, so most queries look in one or two cells.</summary>
    private const float CellSize = 2f;

    private readonly List<int>[] _cells;
    private readonly float _minX, _minY;
    private readonly int _columns, _rows;

    /// <summary>Sorts <paramref name="solids"/> into the cells each one overlaps; queries answer with indices into this
    /// list.</summary>
    public SolidGrid(IReadOnlyList<Box> solids)
    {
        float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;
        for (int i = 0; i < solids.Count; i++)
        {
            Box s = solids[i];
            minX = i == 0 ? s.MinX : MathF.Min(minX, s.MinX);
            minY = i == 0 ? s.MinY : MathF.Min(minY, s.MinY);
            maxX = i == 0 ? s.MaxX : MathF.Max(maxX, s.MaxX);
            maxY = i == 0 ? s.MaxY : MathF.Max(maxY, s.MaxY);
        }

        _minX = minX;
        _minY = minY;
        _columns = Math.Max(1, (int)MathF.Ceiling((maxX - minX) / CellSize) + 1);
        _rows = Math.Max(1, (int)MathF.Ceiling((maxY - minY) / CellSize) + 1);
        _cells = new List<int>[_columns * _rows];
        for (int c = 0; c < _cells.Length; c++)
        {
            _cells[c] = new List<int>();
        }

        for (int i = 0; i < solids.Count; i++)
        {
            Box s = solids[i];
            (int x0, int y0, int x1, int y1) = Range(s);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    _cells[y * _columns + x].Add(i); // ascending: solids are added in order
                }
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="near"/> with the indices of every solid that may overlap <paramref name="area"/> (and a few that don't),
    /// ascending and each once: the order the solids would be tested in without the grid, so ties fall the same way.
    /// </summary>
    public void Near(Box area, List<int> near)
    {
        near.Clear();
        (int x0, int y0, int x1, int y1) = Range(area);
        if (x0 == x1 && y0 == y1)
        {
            near.AddRange(_cells[y0 * _columns + x0]);
            return;
        }

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                near.AddRange(_cells[y * _columns + x]);
            }
        }

        near.Sort();
        int kept = 0;
        for (int k = 0; k < near.Count; k++)
        {
            if (kept == 0 || near[kept - 1] != near[k])
            {
                near[kept++] = near[k];
            }
        }

        near.RemoveRange(kept, near.Count - kept);
    }

    /// <summary>The cells a box covers, clamped to the grid (whatever lies outside the solids' extent holds none).</summary>
    private (int X0, int Y0, int X1, int Y1) Range(Box b) =>
        (Column(b.MinX), Row(b.MinY), Column(b.MaxX), Row(b.MaxY));

    private int Column(float x) => Math.Clamp((int)MathF.Floor((x - _minX) / CellSize), 0, _columns - 1);

    private int Row(float y) => Math.Clamp((int)MathF.Floor((y - _minY) / CellSize), 0, _rows - 1);
}
