using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Mag-grip surface graph for <see cref="ReachPlanner"/>: samples every metal face where the body fits, unions walkable
/// connected components (along faces and around corners of touching metal solids), and walks a reached part to touch triggers,
/// stand up on tops and launch off walls and ceilings.
/// </summary>
internal sealed class GripGraph
{
    /// <summary>Spacing of the grip poses along a face, meters.</summary>
    private const float GripStep = 0.25f;

    /// <summary>
    /// Two grip poses this close (not on opposite faces, and on one metal solid or on two that meet) are one walk apart: along a face,
    /// or round a corner.
    /// </summary>
    private const float GripLink = 0.8f;

    /// <summary>A pose on the face of the metal solid <paramref name="Solid"/> (an index into the solids).</summary>
    private readonly record struct GripNode(Vector2 Feet, Vector2 Up, bool Launch, int Solid, int Part);

    private readonly SolidSpace _space;
    private readonly WaterGraph? _water;
    private readonly HashSet<int> _metal;
    private readonly List<GripNode> _nodes = new();
    private readonly List<List<int>> _parts = new();
    private readonly Dictionary<(int X, int Y), List<int>> _cells = new();

    private GripGraph(GripLimits limits, SolidSpace space, WaterGraph? water, HashSet<int> metal)
    {
        Limits = limits;
        _space = space;
        _water = water;
        _metal = metal;
        BuildParts();
    }

    public GripLimits Limits { get; }

    /// <summary>Builds a grip graph if <paramref name="limits"/> and any matching <paramref name="metal"/> solids are present; otherwise null.</summary>
    public static GripGraph? Build(GripLimits? limits, IEnumerable<Box>? metal, SolidSpace space, WaterGraph? water)
    {
        if (limits is null || metal is null)
        {
            return null;
        }

        var metalBoxes = metal.ToHashSet();
        var indices = new HashSet<int>();
        for (int i = 0; i < space.Solids.Count; i++)
        {
            if (metalBoxes.Contains(space.Solids[i]))
            {
                indices.Add(i);
            }
        }

        return indices.Count > 0 ? new GripGraph(limits, space, water, indices) : null;
    }

    /// <summary>Whether solid index <paramref name="solid"/> is grippable metal.</summary>
    public bool IsMetal(int solid) => _metal.Contains(solid);

    /// <summary>The connected part index of the grip pose with <paramref name="up"/> nearest to <paramref name="feet"/> (within 1 m), or −1.</summary>
    public int PartNear(Vector2 feet, Vector2 up)
    {
        int best = -1;
        float bestDistance = 1f;
        int cx = (int)MathF.Floor(feet.X), cy = (int)MathF.Floor(feet.Y);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!_cells.TryGetValue((cx + dx, cy + dy), out List<int>? cell))
                {
                    continue;
                }

                foreach (int n in cell)
                {
                    float d = Vector2.Distance(_nodes[n].Feet, feet);
                    if (_nodes[n].Up == up && d <= bestDistance)
                    {
                        best = n;
                        bestDistance = d;
                    }
                }
            }
        }

        return best >= 0 ? _nodes[best].Part : -1;
    }

    /// <summary>
    /// Gripping a part of the metal surface: walking all of it, touching what the body passes, standing up on its tops, and leaping
    /// off (or letting go of) its walls and dropping from under its ceilings, every meter or so.
    /// </summary>
    public void Walk(int part, IReadOnlyList<Box> triggers, HashSet<int> reachedTriggers, Func<Vector2, int> ledgeAt,
        Action<int> land, Action<Vector2, Vector2, int, int, int> launch)
    {
        int regrab = (int)MathF.Ceiling(Limits.RegrabDelay / ReachPlanner.Tick);
        float centerHeight = _space.Limits.Height * 0.5f;
        foreach (int n in _parts[part])
        {
            GripNode node = _nodes[n];
            Box body = GripBody(node.Feet, node.Up);
            for (int t = 0; t < triggers.Count; t++)
            {
                if (body.Overlaps(triggers[t]))
                {
                    reachedTriggers.Add(t);
                }
            }

            if (node.Up.Y > 0.5f)
            {
                land(ledgeAt(node.Feet));
                continue;
            }

            if (!node.Launch)
            {
                continue;
            }

            Vector2 upright = GripMath.Upright(node.Feet, node.Up, centerHeight);
            foreach (int dir in new[] { -1, 0, 1 })
            {
                if (node.Up.Y < -0.5f)
                {
                    // Under a ceiling: the leap pushes off downward.
                    foreach (int dash in new[] { -1, 8 })
                    {
                        launch(upright, new Vector2(dir * _space.Limits.RunSpeed, -Limits.DropSpeed), dir, dash, regrab);
                    }

                    continue;
                }

                var leap = new Vector2(node.Up.X * Limits.LeapAwaySpeed, _space.Limits.TakeOff * Limits.LeapUpFactor);
                foreach (int dash in new[] { -1, 10 })
                {
                    launch(upright, leap, dir, dash, regrab);
                }

                launch(upright, node.Up * Limits.LetGoSpeed, dir, -1, regrab);
            }
        }
    }

    /// <summary>The body lying along <paramref name="up"/> from feet on a surface (upright on a floor).</summary>
    private Box GripBody(Vector2 feet, Vector2 up) =>
        Box.Around(feet + up * (_space.Limits.Height * 0.5f),
            MathF.Abs(up.X) > 0.5f ? new Vector2(_space.Limits.Height, _space.Limits.Width) : new Vector2(_space.Limits.Width, _space.Limits.Height));

    private void BuildParts()
    {
        foreach (int i in _metal.Order())
        {
            Box b = _space.Solids[i];
            var faces = new (Vector2 Up, Vector2 From, Vector2 To)[]
            {
                (new Vector2(-1f, 0f), new Vector2(b.MinX, b.MinY), new Vector2(b.MinX, b.MaxY)),
                (new Vector2(1f, 0f), new Vector2(b.MaxX, b.MinY), new Vector2(b.MaxX, b.MaxY)),
                (new Vector2(0f, -1f), new Vector2(b.MinX, b.MinY), new Vector2(b.MaxX, b.MinY)),
                (new Vector2(0f, 1f), new Vector2(b.MinX, b.MaxY), new Vector2(b.MaxX, b.MaxY)),
            };
            foreach ((Vector2 up, Vector2 from, Vector2 to) in faces)
            {
                int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(from, to) / GripStep));
                for (int k = 0; k <= steps; k++)
                {
                    Vector2 feet = Vector2.Lerp(from, to, k / (float)steps);
                    Box body = GripBody(feet, up);
                    var inner = new Box(body.MinX + SolidSpace.Skin, body.MinY + SolidSpace.Skin, body.MaxX - SolidSpace.Skin, body.MaxY - SolidSpace.Skin);
                    if (_space.Blocked(inner) || (_water is not null && _water.Drowned(feet + up * (_space.Limits.Height * 0.5f))))
                    {
                        continue;
                    }

                    var key = ((int)MathF.Floor(feet.X), (int)MathF.Floor(feet.Y));
                    if (!_cells.TryGetValue(key, out List<int>? cell))
                    {
                        _cells[key] = cell = new List<int>();
                    }

                    cell.Add(_nodes.Count);
                    _nodes.Add(new GripNode(feet, up, k % 4 == 0 || k == steps, i, -1));
                }
            }
        }

        // Whether the grip goes on from one metal solid onto the other: the same, or two sharing an edge (or more), with nothing between.
        bool Meet(int a, int b)
        {
            Box p = _space.Solids[a], q = _space.Solids[b];
            float x = MathF.Min(p.MaxX, q.MaxX) - MathF.Max(p.MinX, q.MinX), y = MathF.Min(p.MaxY, q.MaxY) - MathF.Max(p.MinY, q.MinY);
            return a == b || (x >= -SolidSpace.Skin && y >= -SolidSpace.Skin && (x > SolidSpace.Skin || y > SolidSpace.Skin));
        }

        // Union the poses a short walk apart on metal that holds all the way (rock between two solids ends the grip; a gap is taken as its end too).
        int[] root = Enumerable.Range(0, _nodes.Count).ToArray();
        int Find(int x)
        {
            while (root[x] != x)
            {
                x = root[x] = root[root[x]];
            }

            return x;
        }

        for (int n = 0; n < _nodes.Count; n++)
        {
            GripNode a = _nodes[n];
            int cx = (int)MathF.Floor(a.Feet.X), cy = (int)MathF.Floor(a.Feet.Y);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!_cells.TryGetValue((cx + dx, cy + dy), out List<int>? cell))
                    {
                        continue;
                    }

                    foreach (int m in cell.Where(m => m > n))
                    {
                        GripNode b = _nodes[m];
                        if (Vector2.Dot(a.Up, b.Up) > -0.5f && Vector2.Distance(a.Feet, b.Feet) <= GripLink && Meet(a.Solid, b.Solid))
                        {
                            root[Find(m)] = Find(n);
                        }
                    }
                }
            }
        }

        var parts = new Dictionary<int, int>();
        for (int n = 0; n < _nodes.Count; n++)
        {
            int r = Find(n);
            if (!parts.TryGetValue(r, out int part))
            {
                parts[r] = part = _parts.Count;
                _parts.Add(new List<int>());
            }

            _parts[part].Add(n);
            _nodes[n] = _nodes[n] with { Part = part };
        }
    }
}
