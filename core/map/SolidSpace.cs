using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The solid and hazard geometry of one room for <see cref="ReachPlanner"/>: axis-aligned box queries, single-axis motion,
/// line of sight, ledge discovery, trigger overlaps and ledge assist. Thread-safe once built (each thread keeps its own
/// scratch list of nearby solids).
/// </summary>
internal sealed class SolidSpace
{
    /// <summary>Clearance kept off solid faces during single-axis sweeps, meters.</summary>
    public const float Skin = 0.001f;

    /// <summary>How far past half the body's width a landing's feet may be beyond a ledge's end and still be on it, meters.</summary>
    private const float LandingMargin = 0.05f;

    /// <summary>Each thread's list of solids near a moving body (walks of one planner run on several threads at once).</summary>
    [ThreadStatic]
    private static List<int>? _near;

    private readonly SolidGrid _grid;

    public SolidSpace(MoveLimits limits, List<Box> solids, List<Box> hazards)
    {
        Limits = limits;
        Solids = solids;
        Hazards = hazards;
        _grid = new SolidGrid(solids);
    }

    public MoveLimits Limits { get; }
    public List<Box> Solids { get; }
    public List<Box> Hazards { get; }

    /// <summary>The upright body box with its feet at <paramref name="feet"/>.</summary>
    public Box BodyAt(Vector2 feet) =>
        new(feet.X - Limits.Width * 0.5f, feet.Y, feet.X + Limits.Width * 0.5f, feet.Y + Limits.Height);

    /// <summary>Whether <paramref name="box"/> overlaps any solid or hazard.</summary>
    public bool Blocked(Box box) => Solids.Any(s => s.Overlaps(box)) || Hazards.Any(h => h.Overlaps(box));

    /// <summary>Whether the body with its feet at <paramref name="p"/> touches a hazard.</summary>
    public bool InHazard(Vector2 p)
    {
        if (Hazards.Count == 0)
        {
            return false;
        }

        Box body = BodyAt(p);
        foreach (Box h in Hazards)
        {
            if (h.Overlaps(body))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Records every trigger overlapped by the upright body with its feet at <paramref name="p"/>.</summary>
    public void Touch(Vector2 p, IReadOnlyList<Box> triggers, HashSet<int> reached)
    {
        Box body = BodyAt(p);
        for (int t = 0; t < triggers.Count; t++)
        {
            if (body.Overlaps(triggers[t]))
            {
                reached.Add(t);
            }
        }
    }

    /// <summary>Moves the body along one axis by <paramref name="delta"/>, stopping against the first solid.</summary>
    public float MoveAxis(Vector2 p, float delta, bool horizontal, out bool hit, out int hitSolid)
    {
        hit = false;
        hitSolid = -1;
        Box body = BodyAt(p);
        float moved = delta;
        List<int> near = _near ??= new List<int>();
        const float margin = 0.01f; // past the skin either side: every solid the tests below could stop at
        _grid.Near(horizontal
            ? new Box(MathF.Min(body.MinX, body.MinX + delta) - margin, body.MinY - margin, MathF.Max(body.MaxX, body.MaxX + delta) + margin, body.MaxY + margin)
            : new Box(body.MinX - margin, MathF.Min(body.MinY, body.MinY + delta) - margin, body.MaxX + margin, MathF.Max(body.MaxY, body.MaxY + delta) + margin),
            near);
        foreach (int i in near)
        {
            Box s = Solids[i];
            if (horizontal)
            {
                if (body.MinY >= s.MaxY - Skin || body.MaxY <= s.MinY + Skin)
                {
                    continue;
                }

                if (delta > 0f && body.MaxX <= s.MinX + Skin && body.MaxX + moved > s.MinX)
                {
                    moved = s.MinX - body.MaxX - Skin;
                    hit = true;
                    hitSolid = i;
                }
                else if (delta < 0f && body.MinX >= s.MaxX - Skin && body.MinX + moved < s.MaxX)
                {
                    moved = s.MaxX - body.MinX + Skin;
                    hit = true;
                    hitSolid = i;
                }
            }
            else
            {
                if (body.MinX >= s.MaxX - Skin || body.MaxX <= s.MinX + Skin)
                {
                    continue;
                }

                if (delta > 0f && body.MaxY <= s.MinY + Skin && body.MaxY + moved > s.MinY)
                {
                    moved = s.MinY - body.MaxY - Skin;
                    hit = true;
                    hitSolid = i;
                }
                else if (delta < 0f && body.MinY >= s.MaxY - Skin && body.MinY + moved < s.MaxY)
                {
                    moved = s.MaxY - body.MinY + Skin;
                    hit = true;
                    hitSolid = i;
                }
            }
        }

        return (horizontal ? p.X : p.Y) + moved;
    }

    /// <summary>
    /// Ledge assist (<see cref="MoveLimits.LedgeAssist"/>): a body with its feet at <paramref name="p"/>, against the side of
    /// <paramref name="wall"/> moving <paramref name="dir"/>, whose top is a little above the feet: the feet on the top, just past
    /// the edge, if the body fits there; otherwise null.
    /// </summary>
    public Vector2? LedgeAssist(Vector2 p, int dir, Box wall)
    {
        float rise = wall.MaxY - p.Y;
        if (Limits.LedgeAssist <= 0f || rise <= 0f || rise > Limits.LedgeAssist)
        {
            return null;
        }

        var lifted = new Vector2(p.X + dir * TheDeep.LedgeAssist.Reach, wall.MaxY + Skin);
        Box body = Box.Around(lifted + new Vector2(0f, Limits.Height * 0.5f), new Vector2(Limits.Width, Limits.Height));
        return Solids.Any(s => s.Overlaps(body)) ? null : lifted;
    }

    /// <summary>Whether nothing solid lies between two points on the play plane (stopping a little short of <paramref name="to"/>).</summary>
    public bool InSight(Vector2 from, Vector2 to)
    {
        float length = Vector2.Distance(from, to) - HarpoonAim.SightMargin;
        if (length <= 0f)
        {
            return true;
        }

        Vector2 dir = Vector2.Normalize(to - from), end = from + dir * length;
        List<int> near = _near ??= new List<int>();
        _grid.Near(new Box(MathF.Min(from.X, end.X), MathF.Min(from.Y, end.Y), MathF.Max(from.X, end.X), MathF.Max(from.Y, end.Y)), near);
        for (float t = 0f; t <= length; t += 0.1f)
        {
            Vector2 p = from + dir * t;
            foreach (int i in near)
            {
                Box s = Solids[i];
                if (p.X > s.MinX && p.X < s.MaxX && p.Y > s.MinY && p.Y < s.MaxY)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The standing ranges on every solid's top: where the feet can be with the body clear of other solids
    /// (split where a solid sits on top or overhangs within a body's height).
    /// </summary>
    public List<Ledge> FindLedges()
    {
        var ledges = new List<Ledge>();
        foreach (Box s in Solids)
        {
            foreach ((float from, float to) in Clear(s.MinX, s.MaxX, s.MaxY))
            {
                ledges.Add(new Ledge(s.MaxY, from, to));
            }
        }

        return ledges;
    }

    /// <summary>
    /// The stretches of [<paramref name="from"/>, <paramref name="to"/>] where feet at height <paramref name="y"/> keep the body clear
    /// of every solid and hazard, wider than 5 cm.
    /// </summary>
    public IEnumerable<(float From, float To)> Clear(float from, float to, float y)
    {
        float half = Limits.Width * 0.5f;
        var body = new Box(from - half, y + Skin, to + half, y + Limits.Height);
        IEnumerable<(float, float)> blocked = Solids.Concat(Hazards).Where(o => o.Overlaps(body)).Select(o => (o.MinX - half, o.MaxX + half));
        return Spans.Gaps(blocked, from, to).Where(f => f.To - f.From > 0.05f);
    }

    /// <summary>
    /// The ledge (or with <paramref name="swim"/>, the float line) whose range holds feet at <paramref name="p"/>, or −1: feet within half
    /// the body's width (and <see cref="LandingMargin"/>) past an end still stand on it, the body's edge over the floor.
    /// </summary>
    public int LedgeAt(IReadOnlyList<Ledge> ledges, Vector2 p, bool swim = false)
    {
        float tolerance = swim ? 0.1f : 0.01f;
        float overhang = Limits.Width * 0.5f + LandingMargin;
        for (int i = 0; i < ledges.Count; i++)
        {
            Ledge l = ledges[i];
            if (l.Swim == swim && MathF.Abs(l.Y - p.Y) < tolerance && p.X >= l.MinX - overhang && p.X <= l.MaxX + overhang)
            {
                return i;
            }
        }

        return -1;
    }
}
