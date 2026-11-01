using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>The base moveset's numbers (copied from <c>player_tuning.tres</c>, the single source), as the planner simulates them.</summary>
public sealed record MoveLimits(
    float RunSpeed, float AirAcceleration, float AirDeceleration, float JumpHeight, float JumpTimeToApex, float JumpCutFactor,
    float FallGravityMultiplier, float ApexHangThreshold, float ApexGravityMultiplier, float MaxFallSpeed,
    float DashDistance, float DashDuration, float DashExitSpeedFactor, int AirDashes,
    float Width = 0.7f, float Height = 1.6f, float EdgeMargin = 0f, float LedgeAssist = 0f)
{
    /// <summary>
    /// A clumsier pilgrim, for the slack report: jumps 18% lower, dashes 35% shorter, runs 20% slower, never takes off from the very
    /// end of a ledge, and gets no ledge assist (it still climbs 2.5 m steps and crosses 7.5 m gaps). A way only these limits miss asks
    /// for too exact play (owner 2026-11-01: jumps must not be exact; the bar was 10/15/8% until then).
    /// </summary>
    public MoveLimits Clumsy() => this with
    {
        JumpHeight = JumpHeight * 0.82f, DashDistance = DashDistance * 0.65f, RunSpeed = RunSpeed * 0.8f, EdgeMargin = 0.3f, LedgeAssist = 0f,
    };

    public float Gravity => JumpMath.Gravity(JumpHeight, JumpTimeToApex);
    public float TakeOff => JumpMath.TakeOffSpeed(JumpHeight, JumpTimeToApex);
}

/// <summary>
/// The Harpoon's numbers (from <c>player_tuning.tres</c>), for a planner that may also zip to anchors: range from the body's
/// center, the nearest an anchor may be, the pull's top speed and acceleration, and the release (speed kept, lift).
/// </summary>
public sealed record HarpoonLimits(float Range, float MinDistance, float ZipSpeed, float ZipAcceleration, float ExitSpeedFactor, float ExitLift);

/// <summary>
/// Mag-grip's numbers (from <c>player_tuning.tres</c>), for a planner that may also walk on metal: the leap off a wall (speed away,
/// share of a jump's take-off upward) and how long after letting go the body can't grip again.
/// </summary>
public sealed record GripLimits(float LeapAwaySpeed, float LeapUpFactor, float RegrabDelay);

/// <summary>An axis-aligned solid on the play plane.</summary>
public readonly record struct Box(float MinX, float MinY, float MaxX, float MaxY)
{
    public bool Overlaps(Box o) => MinX < o.MaxX && o.MinX < MaxX && MinY < o.MaxY && o.MinY < MaxY;

    public static Box Around(Vector2 center, Vector2 size) =>
        new(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f, center.X + size.X * 0.5f, center.Y + size.Y * 0.5f);
}

/// <summary>
/// A lift on the play plane: its platform at the lower stop (a solid there), carrying whoever stands on it <see cref="Rise"/> up.
/// </summary>
public readonly record struct LiftPath(Box Platform, float Rise);

/// <summary>
/// A floor the pilgrim can stand on: the top of a solid at <see cref="Y"/>, feet from <see cref="MinX"/> to <see cref="MaxX"/>;
/// or, with <see cref="Swim"/>, a water's float line (the feet of a body floating on it).
/// </summary>
public readonly record struct Ledge(float Y, float MinX, float MaxX, bool Swim = false);

/// <summary>
/// What the base moveset can reach in one room, pure (M7's geometry sanity check, docs/long-term.md): the
/// pilgrim's body as a box walks along ledges and is simulated tick by tick through jumps (full and cut),
/// walk-offs and air dashes, with the real tuning numbers and collisions against the room's solids. From
/// an arrival point it finds every ledge it can stand on and every trigger area (an exit) its body can
/// enter. Conservative where it must be: the body is a box (the real capsule slips off edges a little
/// more easily), take-offs are sampled (ledge ends and every 2 m), so a gap the planner can't cross may
/// still be crossable by a hair; the check treats "reached" as proof, "not reached" as the designer's claim.
/// Given <see cref="HarpoonLimits"/> and anchors, it also fires the Harpoon: from standing (and from the top of a
/// full jump) at every take-off point, and from each anchor reached, at every anchor in range and in clear sight,
/// then lets go there (drifting either way or not, jumping off or not, dashing or not).
/// Lifts (<see cref="LiftPath"/>) stand at their lower stop as solids; landing on one rides it up, the body touching every
/// trigger it passes through on the way (stepping off at the top is not simulated: keep a lift's way out in its path).
/// Water (boxes whose top is the surface, with <see cref="WaterLimits"/>): a body whose feet go deep enough swims with the
/// pilgrim's own water motion (<see cref="WaterMotion"/>): it floats, dips after a plunge and bobs back to its float line,
/// which is a ledge to swim along and jump from. Floors deeper than a jump out of the water are not take-off points. With
/// <c>ballast</c>, a body in water reaches every point of the water connected to where it went in (sinking and rising at
/// will), touching what the body passes and standing on every floor it finds there.
/// Mag-grip (<see cref="GripLimits"/> and the metal solids): a body pushing into a metal wall (walking, jumping or falling, not
/// dashing) or rising into a metal ceiling grips it, and then walks the whole surface connected to that point: along each face,
/// around a metal block's corners, onto a metal wall met at an inner corner, standing up on a top; it touches what its body
/// passes (lying along the normal), and from anywhere on a wall it leaps off or lets go, from anywhere under a ceiling it drops.
/// Only metal holds (rock going on in the same plane is the surface's end), and water lets go of a body whose center it drowns
/// (<see cref="GripMath.Drowned"/>): no grip pose lies there.
/// Ledge assist: an airborne body (not dashing) pressing into a wall whose top is at most <see cref="MoveLimits.LedgeAssist"/> above
/// its feet is lifted onto it, as the pilgrim is.
/// Hazards (heat zones, for a planner without Heat plating) are space the body never enters: a trajectory that touches one
/// ends there, and no ledge or water reaches into one.
/// </summary>
public sealed class ReachPlanner
{
    private const float Dt = 1f / 60f;
    private const int MaxTicks = 240;

    /// <summary>Longest a body may float before it settles (rising from the bottom of deep water), ticks.</summary>
    private const int MaxSwimTicks = 900;
    private const float Skin = 0.001f;

    /// <summary>Sight to an anchor stops this short of it (an anchor may hang by a wall's face).</summary>
    private const float SightMargin = 0.3f;

    private readonly MoveLimits _m;
    private readonly List<Box> _solids;
    private readonly List<Ledge> _ledges;
    private readonly HarpoonLimits? _harpoon;
    private readonly List<Vector2> _anchors;
    private readonly List<LiftPath> _lifts;
    private readonly WaterLimits? _w;
    private readonly List<Box> _waters;
    private readonly bool _ballast;
    private readonly List<Box> _hazards;
    private readonly GripLimits? _grip;

    /// <summary>The solids (by index) that are metal, with Mag-grip.</summary>
    private readonly HashSet<int> _metal = new();

    /// <summary>With Mag-grip: poses on metal surfaces where the body fits, grouped into the parts walkable together.</summary>
    private readonly List<GripNode> _gripNodes = new();
    private readonly List<List<int>> _gripParts = new();
    private readonly Dictionary<(int X, int Y), List<int>> _gripCells = new();

    /// <summary>Spacing of the grip poses along a face, meters.</summary>
    private const float GripStep = 0.25f;

    /// <summary>Two grip poses this close (and not on opposite faces) are one walk apart: along a face, or round a corner.</summary>
    private const float GripLink = 0.8f;

    private readonly record struct GripNode(Vector2 Feet, Vector2 Up, bool Launch, int Part);

    /// <summary>With Ballast, the water reachable together (feet positions on a grid where the body fits, by connected part).</summary>
    private readonly Dictionary<(int X, int Y), int> _cellPart = new();
    private readonly List<List<Vector2>> _parts = new();

    /// <summary>The grid of feet positions under water, with Ballast.</summary>
    private const float Cell = 0.25f;

    public ReachPlanner(MoveLimits limits, IEnumerable<Box> solids, HarpoonLimits? harpoon = null, IEnumerable<Vector2>? anchors = null,
        IEnumerable<LiftPath>? lifts = null, WaterLimits? water = null, IEnumerable<Box>? waters = null, bool ballast = false,
        IEnumerable<Box>? hazards = null, GripLimits? grip = null, IEnumerable<Box>? metal = null)
    {
        _m = limits;
        _hazards = hazards?.ToList() ?? new List<Box>();
        _lifts = lifts?.ToList() ?? new List<LiftPath>();
        _solids = solids.Concat(_lifts.Select(l => l.Platform)).ToList();
        _w = water;
        _waters = water is null ? new List<Box>() : waters?.ToList() ?? new List<Box>();
        _ballast = ballast && _waters.Count > 0;
        _ledges = FindLedges();
        _ledges.AddRange(FindSwimLedges());
        _harpoon = harpoon;
        _anchors = harpoon is null ? new List<Vector2>() : anchors?.ToList() ?? new List<Vector2>();
        if (_ballast)
        {
            FindWaterParts();
        }

        if (grip is not null && metal is not null)
        {
            var metalBoxes = metal.ToHashSet();
            for (int i = 0; i < _solids.Count; i++)
            {
                if (metalBoxes.Contains(_solids[i]))
                {
                    _metal.Add(i);
                }
            }

            _grip = _metal.Count > 0 ? grip : null;
            if (_grip is not null)
            {
                FindGripParts();
            }
        }
    }

    public IReadOnlyList<Ledge> Ledges => _ledges;

    /// <summary>
    /// The trigger areas (by index into <paramref name="triggers"/>) the base moveset can enter starting at
    /// <paramref name="arrival"/> (feet) with <paramref name="velocity"/>, and the ledges it can stand on.
    /// </summary>
    public (HashSet<int> Triggers, HashSet<int> Ledges) Reach(Vector2 arrival, Vector2 velocity, IReadOnlyList<Box> triggers, int steer = 0)
    {
        var reachedTriggers = new HashSet<int>();
        var reachedLedges = new HashSet<int>();
        var queue = new Queue<int>();
        var reachedAnchors = new HashSet<int>();
        var anchorQueue = new Queue<(int Anchor, Vector2 From)>();

        // The Harpoon from a body center: every anchor in range and in sight.
        void Fire(Vector2 center)
        {
            for (int a = 0; a < _anchors.Count; a++)
            {
                float d = Vector2.Distance(center, _anchors[a]);
                if (d <= _harpoon!.Range && d >= _harpoon.MinDistance && !reachedAnchors.Contains(a) && InSight(center, _anchors[a]))
                {
                    reachedAnchors.Add(a);
                    anchorQueue.Enqueue((a, center));
                }
            }
        }

        var sunkParts = new HashSet<int>();
        var grippedParts = new HashSet<int>();
        var gripQueue = new Queue<int>();

        // With Mag-grip: gripping a metal surface at a pose (feet, up) walks the whole part of the surface connected to it.
        void Grab(Vector2 feet, Vector2 up)
        {
            int node = GripNodeNear(feet, up);
            if (node >= 0 && grippedParts.Add(_gripNodes[node].Part))
            {
                gripQueue.Enqueue(_gripNodes[node].Part);
            }
        }

        // With Ballast: everywhere in the water connected to where the body went in.
        void Sink(Vector2 feet)
        {
            int part = PartNear(feet);
            if (part < 0 || !sunkParts.Add(part))
            {
                return;
            }

            foreach (Vector2 cell in _parts[part])
            {
                Touch(cell, triggers, reachedTriggers);
                for (int i = 0; i < _ledges.Count; i++)
                {
                    Ledge l = _ledges[i];
                    if (MathF.Abs(l.Y - cell.Y) < 0.01f && cell.X >= l.MinX - 0.01f && cell.X <= l.MaxX + 0.01f)
                    {
                        Land(i);
                    }
                }
            }
        }

        void Land(int ledge)
        {
            if (ledge >= 0 && reachedLedges.Add(ledge))
            {
                queue.Enqueue(ledge);
                Ledge l = _ledges[ledge];
                // Standing anywhere along it.
                var walk = new Box(l.MinX - _m.Width * 0.5f, l.Y + Skin, l.MaxX + _m.Width * 0.5f, l.Y + _m.Height);
                for (int t = 0; t < triggers.Count; t++)
                {
                    if (walk.Overlaps(triggers[t]))
                    {
                        reachedTriggers.Add(t);
                    }
                }

                foreach (LiftPath lift in _lifts.Where(f => MathF.Abs(f.Platform.MaxY - l.Y) < 0.01f && l.MinX < f.Platform.MaxX && f.Platform.MinX < l.MaxX))
                {
                    Ride(lift, l, triggers, reachedTriggers);
                }
            }
        }

        // The arrival itself: falling (or rising) from the marker, with and without steering.
        foreach (int dir in steer != 0 ? new[] { steer } : new[] { -1, 0, 1 })
        {
            Simulate(arrival, velocity, dir, jumpTicks: 0, dashTick: -1, triggers, reachedTriggers, Land, Sink, Grab);
        }

        while (queue.Count > 0 || anchorQueue.Count > 0 || gripQueue.Count > 0)
        {
            if (queue.Count == 0 && gripQueue.Count > 0)
            {
                Walk(_gripParts[gripQueue.Dequeue()], triggers, reachedTriggers, Land, Sink, Grab);
                continue;
            }

            if (queue.Count == 0)
            {
                (int a, Vector2 from) = anchorQueue.Dequeue();
                LetGo(_anchors[a], from, triggers, reachedTriggers, Land, Sink, Grab);
                Fire(_anchors[a]);
                continue;
            }

            Ledge l = _ledges[queue.Dequeue()];
            foreach (float x in TakeOffs(l))
            {
                var at = new Vector2(x, l.Y);
                if (!l.Swim && Submerged(at))
                {
                    continue; // under water, deeper than a jump out of it
                }

                if (_anchors.Count > 0)
                {
                    Fire(at + new Vector2(0f, _m.Height * 0.5f));
                    Fire(at + new Vector2(0f, _m.Height * 0.5f + _m.JumpHeight));
                }

                foreach (int dir in new[] { -1, 0, 1 })
                {
                    var running = new Vector2(dir * (l.Swim ? _w!.SwimSpeed : _m.RunSpeed), 0f);
                    foreach (int jump in new[] { -1, 6, 12 })
                    {
                        foreach (int dash in new[] { -1, 6, 14, 22, 30 })
                        {
                            Simulate(at, running, dir, jump, dash, triggers, reachedTriggers, Land, Sink, Grab);
                        }
                    }

                    // Walking off the end instead of jumping (the sim starts with vy = 0 and gravity takes over past the edge).
                    if (dir != 0 && !l.Swim)
                    {
                        foreach (int dash in new[] { -1, 4, 12, 20 })
                        {
                            Simulate(at, running, dir, jumpTicks: 0, dash, triggers, reachedTriggers, Land, Sink, Grab);
                        }
                    }
                }
            }
        }

        return (reachedTriggers, reachedLedges);
    }

    /// <summary>
    /// Gripping a part of the metal surface: walking all of it, touching what the body passes, standing up on its tops, and leaping
    /// off (or letting go of) its walls and dropping from under its ceilings, every meter or so.
    /// </summary>
    private void Walk(List<int> part, IReadOnlyList<Box> triggers, HashSet<int> reachedTriggers, Action<int> land, Action<Vector2> sink,
        Action<Vector2, Vector2> grab)
    {
        GripLimits g = _grip!;
        int regrab = (int)MathF.Ceiling(g.RegrabDelay / Dt);
        float centerHeight = _m.Height * 0.5f;
        foreach (int n in part)
        {
            GripNode node = _gripNodes[n];
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
                land(LedgeAt(node.Feet));
                continue;
            }

            if (!node.Launch)
            {
                continue;
            }

            // Upright again, turned about the body's center (GripMath.Upright).
            Vector2 upright = node.Feet + node.Up * centerHeight - new Vector2(0f, centerHeight);
            foreach (int dir in new[] { -1, 0, 1 })
            {
                if (node.Up.Y < -0.5f)
                {
                    // Under a ceiling: the leap pushes off downward.
                    foreach (int dash in new[] { -1, 8 })
                    {
                        Simulate(upright, new Vector2(dir * _m.RunSpeed, -2f), dir, 0, dash, triggers, reachedTriggers, land, sink, grab,
                            airborne: true, gripFrom: regrab);
                    }

                    continue;
                }

                var leap = new Vector2(node.Up.X * g.LeapAwaySpeed, _m.TakeOff * g.LeapUpFactor);
                foreach (int dash in new[] { -1, 10 })
                {
                    Simulate(upright, leap, dir, 0, dash, triggers, reachedTriggers, land, sink, grab, airborne: true, gripFrom: regrab);
                }

                Simulate(upright, node.Up * 1.5f, dir, 0, -1, triggers, reachedTriggers, land, sink, grab, airborne: true, gripFrom: regrab);
            }
        }
    }

    /// <summary>Whether a gripping body centered at <paramref name="center"/> is drowned in some water (water lets go of it).</summary>
    private bool Drowned(Vector2 center) =>
        _w is not null && _waters.Any(w => center.X > w.MinX && center.X < w.MaxX && center.Y > w.MinY && GripMath.Drowned(center.Y, w.MaxY, _w));

    /// <summary>The body lying along <paramref name="up"/> from feet on a surface (upright on a floor).</summary>
    private Box GripBody(Vector2 feet, Vector2 up) =>
        Box.Around(feet + up * (_m.Height * 0.5f), MathF.Abs(up.X) > 0.5f ? new Vector2(_m.Height, _m.Width) : new Vector2(_m.Width, _m.Height));

    /// <summary>The grip pose with this <paramref name="up"/> nearest to <paramref name="feet"/> (within a meter), or −1.</summary>
    private int GripNodeNear(Vector2 feet, Vector2 up)
    {
        int best = -1;
        float bestDistance = 1f;
        int cx = (int)MathF.Floor(feet.X), cy = (int)MathF.Floor(feet.Y);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!_gripCells.TryGetValue((cx + dx, cy + dy), out List<int>? cell))
                {
                    continue;
                }

                foreach (int n in cell)
                {
                    float d = Vector2.Distance(_gripNodes[n].Feet, feet);
                    if (_gripNodes[n].Up == up && d <= bestDistance)
                    {
                        best = n;
                        bestDistance = d;
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// With Mag-grip: every pose on a metal face (every <see cref="GripStep"/>) where the body fits, linked to the poses a short walk
    /// away (along the face, round a block's outer corner, onto a wall at an inner corner, up onto a top), grouped into parts.
    /// </summary>
    private void FindGripParts()
    {
        foreach (int i in _metal.Order())
        {
            Box b = _solids[i];
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
                    var inner = new Box(body.MinX + Skin, body.MinY + Skin, body.MaxX - Skin, body.MaxY - Skin);
                    if (_solids.Any(s => s.Overlaps(inner)) || _hazards.Any(h => h.Overlaps(inner)) || Drowned(feet + up * (_m.Height * 0.5f)))
                    {
                        continue;
                    }

                    var key = ((int)MathF.Floor(feet.X), (int)MathF.Floor(feet.Y));
                    if (!_gripCells.TryGetValue(key, out List<int>? cell))
                    {
                        _gripCells[key] = cell = new List<int>();
                    }

                    cell.Add(_gripNodes.Count);
                    _gripNodes.Add(new GripNode(feet, up, k % 4 == 0 || k == steps, -1));
                }
            }
        }

        // Union the poses a short walk apart.
        int[] root = Enumerable.Range(0, _gripNodes.Count).ToArray();
        int Find(int x)
        {
            while (root[x] != x)
            {
                x = root[x] = root[root[x]];
            }

            return x;
        }

        for (int n = 0; n < _gripNodes.Count; n++)
        {
            GripNode a = _gripNodes[n];
            int cx = (int)MathF.Floor(a.Feet.X), cy = (int)MathF.Floor(a.Feet.Y);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!_gripCells.TryGetValue((cx + dx, cy + dy), out List<int>? cell))
                    {
                        continue;
                    }

                    foreach (int m in cell.Where(m => m > n))
                    {
                        GripNode b = _gripNodes[m];
                        if (Vector2.Dot(a.Up, b.Up) > -0.5f && Vector2.Distance(a.Feet, b.Feet) <= GripLink)
                        {
                            root[Find(m)] = Find(n);
                        }
                    }
                }
            }
        }

        var parts = new Dictionary<int, int>();
        for (int n = 0; n < _gripNodes.Count; n++)
        {
            int r = Find(n);
            if (!parts.TryGetValue(r, out int part))
            {
                parts[r] = part = _gripParts.Count;
                _gripParts.Add(new List<int>());
            }

            _gripParts[part].Add(n);
            _gripNodes[n] = _gripNodes[n] with { Part = part };
        }
    }

    /// <summary>Standing on a lift's platform anywhere along <paramref name="on"/>, carried up the whole of its rise.</summary>
    private void Ride(LiftPath lift, Ledge on, IReadOnlyList<Box> triggers, HashSet<int> reachedTriggers)
    {
        float a = MathF.Max(on.MinX, lift.Platform.MinX), b = MathF.Min(on.MaxX, lift.Platform.MaxX);
        for (float x = a; x <= b + 0.001f; x += MathF.Max(0.25f, (b - a) / 8f))
        {
            for (float up = 0f; up <= lift.Rise + 0.001f; up += 0.25f)
            {
                Touch(new Vector2(x, on.Y + MathF.Min(up, lift.Rise)), triggers, reachedTriggers);
            }

            Touch(new Vector2(x, on.Y + lift.Rise), triggers, reachedTriggers);
        }
    }

    /// <summary>Pulled from <paramref name="from"/> to <paramref name="anchor"/> (the body's center), then letting go there.</summary>
    private void LetGo(Vector2 anchor, Vector2 from, IReadOnlyList<Box> triggers, HashSet<int> reachedTriggers, Action<int> land,
        Action<Vector2> sink, Action<Vector2, Vector2> grab)
    {
        HarpoonLimits h = _harpoon!;
        float distance = Vector2.Distance(from, anchor);
        float speed = MathF.Min(h.ZipSpeed, MathF.Sqrt(2f * h.ZipAcceleration * distance));
        Vector2 release = HarpoonAim.Release(Vector2.Normalize(anchor - from), speed, h.ExitSpeedFactor, h.ExitLift);
        var feet = anchor - new Vector2(0f, _m.Height * 0.5f);
        Touch(feet, triggers, reachedTriggers);
        foreach (int dir in new[] { -1, 0, 1 })
        {
            foreach (int jump in new[] { 0, -1 })
            {
                foreach (int dash in new[] { -1, 6, 14 })
                {
                    Simulate(feet, release, dir, jump, dash, triggers, reachedTriggers, land, sink, grab, airborne: true);
                }
            }
        }
    }

    /// <summary>Whether nothing solid lies between two points on the play plane (stopping a little short of <paramref name="to"/>).</summary>
    private bool InSight(Vector2 from, Vector2 to)
    {
        float length = Vector2.Distance(from, to) - SightMargin;
        if (length <= 0f)
        {
            return true;
        }

        Vector2 dir = Vector2.Normalize(to - from);
        for (float t = 0f; t <= length; t += 0.1f)
        {
            Vector2 p = from + dir * t;
            if (_solids.Any(s => p.X > s.MinX && p.X < s.MaxX && p.Y > s.MinY && p.Y < s.MaxY))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Where on a ledge to take off: its ends (or <see cref="MoveLimits.EdgeMargin"/> inside them), and every 2 m between.</summary>
    private IEnumerable<float> TakeOffs(Ledge l)
    {
        float a = l.MinX, b = l.MaxX;
        if (b - a > 2f * _m.EdgeMargin)
        {
            a += _m.EdgeMargin;
            b -= _m.EdgeMargin;
        }
        else
        {
            a = b = (a + b) * 0.5f;
        }

        yield return a;
        if (b - a > 0.01f)
        {
            yield return b;
        }

        for (float x = a + 2f; x < b - 1f; x += 2f)
        {
            yield return x;
        }
    }

    /// <summary>
    /// One trajectory: from <paramref name="start"/> (feet) holding <paramref name="dir"/>; jump on the first tick held for
    /// <paramref name="jumpTicks"/> (−1: held to the apex, 0: no jump); an air dash toward <paramref name="dir"/> at
    /// <paramref name="dashTick"/> (−1: none). Ends on landing (reported) or after a time limit. In deep enough water it swims
    /// on, floating, until it settles on the float line (a landing on that swim ledge), or with Ballast reports where it went in.
    /// </summary>
    private void Simulate(Vector2 start, Vector2 velocity, int dir, int jumpTicks, int dashTick, IReadOnlyList<Box> triggers,
        HashSet<int> reachedTriggers, Action<int> land, Action<Vector2> sink, Action<Vector2, Vector2> grab, bool airborne = false,
        int gripFrom = 0)
    {
        Vector2 p = start, v = velocity;
        bool jumping = jumpTicks != 0, cut = false;
        Box? swimming = null;
        airborne |= jumping;
        if (jumping)
        {
            v.Y = _m.TakeOff;
        }

        int dashLeft = 0, dashDir = dir == 0 ? 1 : dir;
        bool dashed = false;
        int limit = MaxTicks;
        for (int tick = 0; tick < limit; tick++)
        {
            bool held = jumpTicks < 0 || tick < jumpTicks;
            if (swimming is null && v.Y <= 0f && WaterAt(p) is { } water && WaterMotion.Deep(p.Y, water.MaxY, _w!))
            {
                if (_ballast)
                {
                    sink(p);
                    return;
                }

                swimming = water;
                limit = tick + MaxSwimTicks; // floating up from deep water takes a while
                dashLeft = 0;
                v.Y = WaterMotion.Splash(v.Y, _w!);
            }

            if (swimming is { } w)
            {
                if (WaterAt(p) is null)
                {
                    swimming = null; // swum out of its side
                }
                else
                {
                    if (Swim(ref p, ref v, dir, w, triggers, reachedTriggers, land))
                    {
                        return;
                    }

                    continue;
                }
            }

            if (dashTick >= 0 && tick == dashTick && !dashed && _m.AirDashes > 0 && airborne)
            {
                dashed = true;
                dashLeft = (int)MathF.Round(_m.DashDuration / Dt);
            }

            if (dashLeft > 0)
            {
                v = new Vector2(dashDir * _m.DashDistance / _m.DashDuration, 0f);
                if (--dashLeft == 0)
                {
                    v = new Vector2(dashDir * _m.RunSpeed * _m.DashExitSpeedFactor, 0f);
                }
            }
            else
            {
                if (jumping && !cut && !held && v.Y > 0f)
                {
                    v.Y *= _m.JumpCutFactor;
                    cut = true;
                }

                float target = dir * _m.RunSpeed;
                bool accelerating = target != 0f && (v.X == 0f || MathF.Sign(target) == MathF.Sign(v.X)) && MathF.Abs(v.X) <= _m.RunSpeed;
                v.X = Motion.Approach(v.X, target, (accelerating ? _m.AirAcceleration : _m.AirDeceleration) * Dt);
                float g = _m.Gravity;
                if (held && jumping && MathF.Abs(v.Y) < _m.ApexHangThreshold)
                {
                    g *= _m.ApexGravityMultiplier;
                }
                else if (v.Y < 0f)
                {
                    g *= _m.FallGravityMultiplier;
                }

                v.Y = MathF.Max(v.Y - g * Dt, -_m.MaxFallSpeed);
            }

            // Move X, then Y, stopping at faces.
            bool grips = _grip is not null && dashLeft == 0 && tick >= gripFrom;
            float pushX = v.X;
            p.X = MoveAxis(p, v.X * Dt, horizontal: true, out bool hitX, out int solidX);
            if (hitX && dashLeft == 0 && dir != 0 && MathF.Sign(pushX) == dir && airborne && LedgeAssist(p, dir, _solids[solidX]) is { } lifted)
            {
                // Just short of a ledge's top: lifted onto it, keeping the way and the speed (Player.LedgeAssist).
                p = lifted;
                v = new Vector2(dir * MathF.Max(MathF.Abs(pushX), AssistSpeed * _m.RunSpeed), 0f);
                hitX = false;
            }

            if (hitX)
            {
                v.X = 0f;
                if (grips && dir != 0 && MathF.Sign(pushX) == dir && _metal.Contains(solidX))
                {
                    // Pushing into a metal wall: onto it, the body turning about its center (GripMath.OntoWall). The trajectory goes
                    // on too, sliding down the wall (a pilgrim that lets the stick go a moment doesn't grip).
                    Box wall = _solids[solidX];
                    grab(new Vector2(dir > 0 ? wall.MinX : wall.MaxX, p.Y + _m.Height * 0.5f), new Vector2(-dir, 0f));
                }
            }

            float before = p.Y;
            float pushY = v.Y;
            p.Y = MoveAxis(p, v.Y * Dt, horizontal: false, out bool hitY, out int solidY);
            if (hitY && grips && pushY > 0f && _metal.Contains(solidY))
            {
                // Rising into a metal ceiling: hanging under it (GripMath.OntoCeiling); or not, bumping it (as above).
                grab(new Vector2(p.X, _solids[solidY].MinY), new Vector2(0f, -1f));
            }

            if (MathF.Abs(p.Y - before) > 1e-5f)
            {
                airborne = true;
            }

            Touch(p, triggers, reachedTriggers);
            if (InHazard(p))
            {
                return;
            }

            if (hitY && v.Y < 0f)
            {
                // On a floor: a landing after a flight ends the trajectory; before take-off (walking to an edge) it goes on.
                land(LedgeAt(p));
                if (airborne || dir == 0 || hitX)
                {
                    return;
                }

                v.Y = 0f;
            }
            else if (hitY)
            {
                v.Y = 0f;
            }
        }
    }

    /// <summary>
    /// Ledge assist (<see cref="MoveLimits.LedgeAssist"/>): a body with its feet at <paramref name="p"/>, against the side of
    /// <paramref name="wall"/> moving <paramref name="dir"/>, whose top is a little above the feet: the feet on the top, just past
    /// the edge, if the body fits there; otherwise null.
    /// </summary>
    private Vector2? LedgeAssist(Vector2 p, int dir, Box wall)
    {
        float rise = wall.MaxY - p.Y;
        if (_m.LedgeAssist <= 0f || rise <= 0f || rise > _m.LedgeAssist)
        {
            return null;
        }

        var lifted = new Vector2(p.X + dir * AssistReach, wall.MaxY + Skin);
        Box body = Box.Around(lifted + new Vector2(0f, _m.Height * 0.5f), new Vector2(_m.Width, _m.Height));
        return _solids.Any(s => s.Overlaps(body)) ? null : lifted;
    }

    /// <summary>How far past the wall's face ledge assist moves the body (Player.LedgeAssist's), meters.</summary>
    private const float AssistReach = 0.15f;

    /// <summary>The least speed a ledge assist leaves the body with, as a share of the run speed (Player.LedgeAssist's).</summary>
    private const float AssistSpeed = 0.6f;

    /// <summary>One tick floating in <paramref name="water"/>; returns true once settled (on the float line, or wading on a floor).</summary>
    private bool Swim(ref Vector2 p, ref Vector2 v, int dir, Box water, IReadOnlyList<Box> triggers, HashSet<int> reachedTriggers, Action<int> land)
    {
        WaterLimits w = _w!;
        v.X = WaterMotion.Horizontal(v.X, dir, w, Dt);
        v.Y = WaterMotion.Vertical(v.Y, p.Y, water.MaxY, sunk: false, w, Dt);
        p.X = MoveAxis(p, v.X * Dt, horizontal: true, out bool hitX, out _);
        if (hitX)
        {
            v.X = 0f;
        }

        p.Y = MoveAxis(p, v.Y * Dt, horizontal: false, out bool hitY, out _);
        Touch(p, triggers, reachedTriggers);
        if (InHazard(p))
        {
            return true; // burned: the trajectory ends without a landing
        }

        float line = WaterMotion.FloatLine(water.MaxY, w);
        if (hitY && v.Y < 0f && WaterMotion.CanJumpOut(p.Y, water.MaxY, w))
        {
            land(LedgeAt(p)); // wading: standing on a floor shallow enough to jump from
            return true;
        }

        if (hitY)
        {
            v.Y = 0f;
        }

        if (MathF.Abs(p.Y - line) < 0.08f && MathF.Abs(v.Y) < 0.6f)
        {
            land(LedgeAt(p, swim: true));
            return true;
        }

        return false;
    }

    /// <summary>Whether the body with its feet at <paramref name="p"/> touches a hazard.</summary>
    private bool InHazard(Vector2 p)
    {
        if (_hazards.Count == 0)
        {
            return false;
        }

        Box body = BodyAt(p);
        return _hazards.Any(h => h.Overlaps(body));
    }

    /// <summary>The water the feet at <paramref name="p"/> are in, or null.</summary>
    private Box? WaterAt(Vector2 p)
    {
        foreach (Box w in _waters)
        {
            if (p.X >= w.MinX && p.X <= w.MaxX && p.Y >= w.MinY && p.Y < w.MaxY)
            {
                return w;
            }
        }

        return null;
    }

    /// <summary>Whether feet at <paramref name="p"/> are under water deeper than a jump out of it.</summary>
    private bool Submerged(Vector2 p) => WaterAt(p) is { } w && !WaterMotion.CanJumpOut(p.Y, w.MaxY, _w!);

    private void Touch(Vector2 p, IReadOnlyList<Box> triggers, HashSet<int> reached)
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

    private Box BodyAt(Vector2 feet) => new(feet.X - _m.Width * 0.5f, feet.Y, feet.X + _m.Width * 0.5f, feet.Y + _m.Height);

    /// <summary>Moves the body along one axis by <paramref name="delta"/>, stopping against the first solid.</summary>
    private float MoveAxis(Vector2 p, float delta, bool horizontal, out bool hit, out int hitSolid)
    {
        hit = false;
        hitSolid = -1;
        Box body = BodyAt(p);
        float moved = delta;
        for (int i = 0; i < _solids.Count; i++)
        {
            Box s = _solids[i];
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

    /// <summary>The ledge (or with <paramref name="swim"/>, the float line) whose range holds feet at <paramref name="p"/>, or −1.</summary>
    private int LedgeAt(Vector2 p, bool swim = false)
    {
        float tolerance = swim ? 0.1f : 0.01f;
        for (int i = 0; i < _ledges.Count; i++)
        {
            Ledge l = _ledges[i];
            if (l.Swim == swim && MathF.Abs(l.Y - p.Y) < tolerance && p.X >= l.MinX - 0.4f && p.X <= l.MaxX + 0.4f)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The standing ranges on every solid's top: where the feet can be with the body clear of other solids
    /// (split where a solid sits on top or overhangs within a body's height).
    /// </summary>
    private List<Ledge> FindLedges()
    {
        var ledges = new List<Ledge>();
        float half = _m.Width * 0.5f;
        foreach (Box s in _solids)
        {
            var free = new List<(float A, float B)> { (s.MinX, s.MaxX) };
            var room = new Box(s.MinX - half, s.MaxY + Skin, s.MaxX + half, s.MaxY + _m.Height);
            foreach (Box o in _solids.Concat(_hazards))
            {
                if (!o.Overlaps(room))
                {
                    continue;
                }

                // Feet can't be where the body would overlap o.
                float cutA = o.MinX - half, cutB = o.MaxX + half;
                free = free.SelectMany(f => Cut(f, cutA, cutB)).ToList();
            }

            foreach ((float a, float b) in free)
            {
                if (b - a > 0.05f)
                {
                    ledges.Add(new Ledge(s.MaxY, a, b));
                }
            }
        }

        return ledges;
    }

    /// <summary>Each water's float line, where a floating body's feet ride: split where solids cut through the body.</summary>
    private List<Ledge> FindSwimLedges()
    {
        var ledges = new List<Ledge>();
        float half = _m.Width * 0.5f;
        foreach (Box water in _waters)
        {
            float y = WaterMotion.FloatLine(water.MaxY, _w!);
            var free = new List<(float A, float B)> { (water.MinX, water.MaxX) };
            var body = new Box(water.MinX - half, y + Skin, water.MaxX + half, y + _m.Height);
            foreach (Box o in _solids.Concat(_hazards).Where(o => o.Overlaps(body)))
            {
                free = free.SelectMany(f => Cut(f, o.MinX - half, o.MaxX + half)).ToList();
            }

            ledges.AddRange(free.Where(f => f.B - f.A > 0.05f).Select(f => new Ledge(y, f.A, f.B, Swim: true)));
        }

        return ledges;
    }

    /// <summary>
    /// With Ballast: the feet positions under each water's float line where the body fits, on a <see cref="Cell"/> grid, grouped
    /// into the parts connected through the water (the body sinks, rises and swims anywhere inside one).
    /// </summary>
    private void FindWaterParts()
    {
        foreach (Box water in _waters)
        {
            float top = WaterMotion.FloatLine(water.MaxY, _w!);
            for (int ix = (int)MathF.Ceiling(water.MinX / Cell); ix * Cell <= water.MaxX; ix++)
            {
                for (int iy = (int)MathF.Ceiling(water.MinY / Cell); iy * Cell <= top + 0.001f; iy++)
                {
                    Box body = BodyAt(new Vector2(ix * Cell, iy * Cell));
                    if (!_solids.Any(s => s.Overlaps(body)) && !_hazards.Any(h => h.Overlaps(body)))
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
            foreach (Ledge l in _ledges.Where(l => l.Swim))
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
    private int PartNear(Vector2 p)
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

    private static IEnumerable<(float, float)> Cut((float A, float B) f, float a, float b)
    {
        if (b <= f.A || a >= f.B)
        {
            yield return f;
            yield break;
        }

        if (a > f.A)
        {
            yield return (f.A, a);
        }

        if (b < f.B)
        {
            yield return (b, f.B);
        }
    }
}
