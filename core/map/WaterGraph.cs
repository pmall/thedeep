using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Water reachability for <see cref="ReachPlanner"/>: swimmable water queries, float lines, buoyancy ticks, drowning checks for
/// Mag-grip, and Ballast connected components of underwater feet positions.
/// </summary>
internal sealed class WaterGraph
{
    /// <summary>The grid of feet positions under water, with Ballast.</summary>
    private const float Cell = 0.25f;

    private readonly Dictionary<(int X, int Y), int> _cellPart = new();
    private readonly List<List<Vector2>> _parts = new();

    public WaterGraph(WaterLimits limits, List<Box> bodies, bool ballast)
    {
        Limits = limits;
        Bodies = bodies;
        Ballast = ballast;
    }

    public WaterLimits Limits { get; }
    public List<Box> Bodies { get; }
    public bool Ballast { get; }

    /// <summary>The water body the feet at <paramref name="p"/> are in, or null; of overlapping bodies, the one whose surface is highest.</summary>
    public Box? WaterAt(Vector2 p)
    {
        Box? hit = null;
        foreach (Box w in Bodies)
        {
            if (p.X >= w.MinX && p.X <= w.MaxX && p.Y >= w.MinY && p.Y < w.MaxY && (hit is not { } h || w.MaxY > h.MaxY))
            {
                hit = w;
            }
        }

        return hit;
    }

    /// <summary>Whether feet at <paramref name="p"/> are under water deeper than a jump out of it.</summary>
    public bool Submerged(Vector2 p) => WaterAt(p) is { } w && !WaterMotion.CanJumpOut(p.Y, w.MaxY, Limits);

    /// <summary>Whether a gripping body centered at <paramref name="center"/> is drowned in some water (water lets go of it).</summary>
    public bool Drowned(Vector2 center) =>
        Bodies.Any(w => center.X > w.MinX && center.X < w.MaxX && center.Y > w.MinY && GripMath.Drowned(center.Y, w.MaxY, Limits));

    /// <summary>Each water's float line, where a floating body's feet ride: split where solids cut through the body.</summary>
    public List<Ledge> FindSwimLedges(SolidSpace space)
    {
        var ledges = new List<Ledge>();
        foreach (Box water in Bodies)
        {
            float y = WaterMotion.FloatLine(water.MaxY, Limits);
            ledges.AddRange(space.Clear(water.MinX, water.MaxX, y).Select(f => new Ledge(y, f.From, f.To, Swim: true)));
        }

        return ledges;
    }

    /// <summary>
    /// With Ballast: the feet positions under each water's float line where the body fits, on a <see cref="Cell"/> grid, grouped
    /// into the parts connected through the water (the body sinks, rises and swims anywhere inside one).
    /// </summary>
    public void BuildParts(SolidSpace space, IReadOnlyList<Ledge> ledges)
    {
        if (!Ballast)
        {
            return;
        }

        foreach (Box water in Bodies)
        {
            float top = WaterMotion.FloatLine(water.MaxY, Limits);
            for (int ix = (int)MathF.Ceiling(water.MinX / Cell); ix * Cell <= water.MaxX; ix++)
            {
                for (int iy = (int)MathF.Ceiling(water.MinY / Cell); iy * Cell <= top + 0.001f; iy++)
                {
                    if (!space.Blocked(space.BodyAt(new Vector2(ix * Cell, iy * Cell))))
                    {
                        _cellPart[(ix, iy)] = -1;
                    }
                }
            }
        }

        foreach ((int, int) start in _cellPart.Keys.ToList())
        {
            if (_cellPart[start] >= 0)
            {
                continue;
            }

            var cells = new List<Vector2>();
            var open = new Queue<(int X, int Y)>();
            _cellPart[start] = _parts.Count;
            open.Enqueue(start);
            while (open.Count > 0)
            {
                (int x, int y) = open.Dequeue();
                cells.Add(new Vector2(x * Cell, y * Cell));
                foreach ((int, int) n in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (_cellPart.TryGetValue(n, out int part) && part < 0)
                    {
                        _cellPart[n] = _parts.Count;
                        open.Enqueue(n);
                    }
                }
            }

            // The float lines over this part: a floating body rests there too.
            foreach (Ledge l in ledges.Where(l => l.Swim))
            {
                for (float x = l.MinX; x <= l.MaxX + 0.001f; x += Cell)
                {
                    if (cells.Any(c => MathF.Abs(c.X - x) < Cell && c.Y <= l.Y && l.Y - c.Y < Cell * 1.5f))
                    {
                        cells.Add(new Vector2(MathF.Min(x, l.MaxX), l.Y));
                    }
                }
            }

            _parts.Add(cells);
        }
    }

    /// <summary>The water part holding (or nearest to) feet at <paramref name="p"/>, or −1.</summary>
    public int PartNear(Vector2 p)
    {
        int cx = (int)MathF.Round(p.X / Cell), cy = (int)MathF.Round(p.Y / Cell);
        for (int r = 0; r <= 4; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (_cellPart.TryGetValue((cx + dx, cy + dy), out int part) && part >= 0)
                    {
                        return part;
                    }
                }
            }
        }

        return -1;
    }

    /// <summary>Every reachable feet position in connected water part <paramref name="part"/>.</summary>
    public IReadOnlyList<Vector2> Cells(int part) => _parts[part];

    /// <summary>One tick floating in the water; returns true once settled (on the float line, or wading on a floor).</summary>
    public bool Swim(SolidSpace space, IReadOnlyList<Ledge> ledges, ref Vector2 p, ref Vector2 v, int dir, Box water,
        IReadOnlyList<Box> triggers, HashSet<int> reachedTriggers, Action<int> land)
    {
        v.X = WaterMotion.Horizontal(v.X, dir, Limits, ReachPlanner.Tick);
        v.Y = WaterMotion.Vertical(v.Y, p.Y, water.MaxY, sunk: false, Limits, ReachPlanner.Tick);
        p.X = space.MoveAxis(p, v.X * ReachPlanner.Tick, horizontal: true, out bool hitX, out _);
        if (hitX)
        {
            v.X = 0f;
        }

        p.Y = space.MoveAxis(p, v.Y * ReachPlanner.Tick, horizontal: false, out bool hitY, out _);
        space.Touch(p, triggers, reachedTriggers);
        if (space.InHazard(p))
        {
            return true; // burned: the trajectory ends without a landing
        }

        float line = WaterMotion.FloatLine(water.MaxY, Limits);
        if (hitY && v.Y < 0f && WaterMotion.CanJumpOut(p.Y, water.MaxY, Limits))
        {
            land(space.LedgeAt(ledges, p)); // wading: standing on a floor shallow enough to jump from
            return true;
        }

        if (hitY)
        {
            v.Y = 0f;
        }

        if (MathF.Abs(p.Y - line) < 0.08f && MathF.Abs(v.Y) < 0.6f)
        {
            land(space.LedgeAt(ledges, p, swim: true));
            return true;
        }

        return false;
    }
}
