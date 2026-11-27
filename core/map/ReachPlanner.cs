using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// What the base moveset can reach in one room, pure (the geometry sanity check, docs/long-term.md#any-order): the
/// pilgrim's body as a box walks along ledges and is simulated tick by tick through jumps (full and cut),
/// walk-offs and air dashes, with the real tuning numbers and collisions against the room's solids (<see cref="SolidSpace"/>).
/// Optional capability graphs extend it when their limits and room geometry are present: <see cref="HarpoonReach"/> for mooring
/// rings, <see cref="WaterGraph"/> for swimming and Ballast, and <see cref="GripGraph"/> for Mag-grip metal surfaces.
/// Conservative where it must be: the body is a box (the real capsule slips off edges a little more easily), take-offs are
/// sampled (ledge ends and every 2 m), so a gap the planner can't cross may still be crossable by a hair; the check treats
/// "reached" as proof, "not reached" as the designer's claim.
/// </summary>
public sealed class ReachPlanner
{
    /// <summary>One tick of the simulation, seconds: a physics frame of the game (Godot's default 60 per second).</summary>
    public const float Tick = 1f / 60f;

    private const int MaxTicks = 240;

    /// <summary>Longest a body may float before it settles (rising from the bottom of deep water), ticks.</summary>
    private const int MaxSwimTicks = 900;

    private readonly SolidSpace _space;
    private readonly List<Ledge> _ledges;

    /// <summary>For each ledge, whether another floor goes on level past its west and its east end (no edge to walk off there).</summary>
    private readonly List<(bool West, bool East)> _goesOn;
    private readonly List<LiftPath> _lifts;
    private readonly HarpoonReach? _harpoon;
    private readonly WaterGraph? _water;
    private readonly GripGraph? _grip;

    /// <summary>A planner for one room: the base moveset's <paramref name="limits"/> over the room's <paramref name="solids"/>.
    /// Each ability is simulated only when both its limits and what it works on are given: the Harpoon with
    /// <paramref name="anchors"/>, swimming with <paramref name="waters"/> (and sinking, with <paramref name="ballast"/>),
    /// Mag-grip with <paramref name="metal"/> solids. Lift platforms count as solids, and <paramref name="hazards"/> are space
    /// the body never enters.</summary>
    public ReachPlanner(MoveLimits limits, IEnumerable<Box> solids, HarpoonLimits? harpoon = null, IEnumerable<Vector2>? anchors = null,
        IEnumerable<LiftPath>? lifts = null, WaterLimits? water = null, IEnumerable<Box>? waters = null, bool ballast = false,
        IEnumerable<Box>? hazards = null, GripLimits? grip = null, IEnumerable<Box>? metal = null)
    {
        _lifts = lifts?.ToList() ?? new List<LiftPath>();
        _space = new SolidSpace(limits, solids.Concat(_lifts.Select(l => l.Platform)).ToList(), hazards?.ToList() ?? new List<Box>());

        List<Box> waterBodies = waters?.ToList() ?? new List<Box>();
        _water = water is not null && waterBodies.Count > 0 ? new WaterGraph(water, waterBodies, ballast) : null;

        _ledges = _space.FindLedges();
        if (_water is not null)
        {
            _ledges.AddRange(_water.FindSwimLedges(_space));
            _water.BuildParts(_space, _ledges);
        }

        _goesOn = _ledges.Select(l => (GoesOn(l, l.MinX - 0.01f), GoesOn(l, l.MaxX + 0.01f))).ToList();

        List<Vector2> anchorList = anchors?.ToList() ?? new List<Vector2>();
        _harpoon = harpoon is not null && anchorList.Count > 0 ? new HarpoonReach(harpoon, anchorList) : null;
        _grip = GripGraph.Build(grip, metal, _space, _water);
    }

    /// <summary>Every floor the pilgrim can stand on in the room, and every water's float line.</summary>
    public IReadOnlyList<Ledge> Ledges => _ledges;

    /// <summary>
    /// The trigger areas (by index into <paramref name="triggers"/>) the base moveset can enter starting at
    /// <paramref name="arrival"/> (feet) with <paramref name="velocity"/>, and the ledges it can stand on.
    /// </summary>
    public (HashSet<int> Triggers, HashSet<int> Ledges) Reach(Vector2 arrival, Vector2 velocity, IReadOnlyList<Box> triggers, int steer = 0) =>
        Reach(new[] { (arrival, velocity, steer) }, triggers);

    /// <summary>
    /// The same from several <paramref name="arrivals"/> at once (feet, velocity, steer: 0 tries every way), each moving as from
    /// <see cref="Reach(Vector2, Vector2, IReadOnlyList{Box}, int)"/>. With <paramref name="leaps"/>, every move of
    /// <see cref="GroundMoves"/> from every take-off point of a floor reached (not a float line) is also given as it flew
    /// (<see cref="Leap"/>), but a move whose air dash never came (still on the floor at its tick): it is the same flight as the one without.
    /// </summary>
    public (HashSet<int> Triggers, HashSet<int> Ledges) Reach(IEnumerable<(Vector2 At, Vector2 Velocity, int Steer)> arrivals, IReadOnlyList<Box> triggers,
        Action<Leap>? leaps = null)
    {
        var reachedTriggers = new HashSet<int>();
        var reachedLedges = new HashSet<int>();
        var queue = new Queue<int>();
        var reachedAnchors = new HashSet<int>();
        var anchorQueue = new Queue<(Vector2 Anchor, Vector2 From)>();
        var sunkParts = new HashSet<int>();
        var grippedParts = new HashSet<int>();
        var gripQueue = new Queue<int>();

        void Grab(GripPose pose)
        {
            if (_grip is not null && _grip.PartNear(pose.Feet, pose.Up) is >= 0 and var part && grippedParts.Add(part))
            {
                gripQueue.Enqueue(part);
            }
        }

        void Sink(Vector2 feet)
        {
            if (_water is null || !_water.Ballast)
            {
                return;
            }

            int part = _water.PartNear(feet);
            if (part < 0 || !sunkParts.Add(part))
            {
                return;
            }

            foreach (Vector2 cell in _water.Cells(part))
            {
                _space.Touch(cell, triggers, reachedTriggers);
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
                var walk = new Box(l.MinX - _space.Limits.Width * 0.5f, l.Y + SolidSpace.Skin, l.MaxX + _space.Limits.Width * 0.5f, l.Y + _space.Limits.Height);
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
        foreach ((Vector2 arrival, Vector2 velocity, int steer) in arrivals)
        {
            foreach (int dir in steer != 0 ? new[] { steer } : new[] { -1, 0, 1 })
            {
                Simulate(arrival, velocity, dir, jumpTicks: 0, dashTick: -1, triggers, reachedTriggers, Land, Sink, Grab);
            }
        }

        while (queue.Count > 0 || anchorQueue.Count > 0 || gripQueue.Count > 0)
        {
            if (queue.Count == 0 && gripQueue.Count > 0)
            {
                int part = gripQueue.Dequeue();
                _grip?.Walk(part, triggers, reachedTriggers, feet => _space.LedgeAt(_ledges, feet), Land,
                    (start, velocity, dir, dash, regrab) =>
                        Simulate(start, velocity, dir, jumpTicks: 0, dash, triggers, reachedTriggers, Land, Sink, Grab, airborne: true, gripFrom: regrab));
                continue;
            }

            if (queue.Count == 0)
            {
                (Vector2 anchor, Vector2 from) = anchorQueue.Dequeue();
                if (_harpoon is { } harpoon)
                {
                    harpoon.LetGo(anchor, from, _space, triggers, reachedTriggers,
                        (start, velocity, dir, jump, dash) =>
                            Simulate(start, velocity, dir, jump, dash, triggers, reachedTriggers, Land, Sink, Grab, airborne: true));
                    harpoon.Fire(anchor, _space, reachedAnchors, anchorQueue);
                }

                continue;
            }

            int ledge = queue.Dequeue();
            Ledge l = _ledges[ledge];
            List<float> takeOffs = TakeOffs(l).ToList();
            float first = takeOffs.Min(), last = takeOffs.Max();
            foreach (float x in takeOffs)
            {
                var at = new Vector2(x, l.Y);
                if (!l.Swim && _water is not null && _water.Submerged(at))
                {
                    continue; // under water, deeper than a jump out of it
                }

                _harpoon?.Fire(at + new Vector2(0f, _space.Limits.Height * 0.5f), _space, reachedAnchors, anchorQueue);

                foreach (GroundMove move in GroundMoves)
                {
                    // A walk-off from the ends, outward, where the floor ends: from further in, it walks there first, and its dash (only
                    // in the air) comes late.
                    if (move.JumpTicks != 0
                        || (!l.Swim && (move.Dir > 0 ? x == last && !_goesOn[ledge].East : x == first && !_goesOn[ledge].West)))
                    {
                        var running = new Vector2(move.Dir * (l.Swim && _water is { } wet ? wet.Limits.SwimSpeed : _space.Limits.RunSpeed), 0f);
                        bool leap = leaps is not null && !l.Swim;
                        bool fires = _harpoon is not null && move == StraightUp;
                        Trace? trace = leap || fires ? new Trace() : null;
                        Simulate(at, running, move.Dir, move.JumpTicks, move.DashTick, triggers, reachedTriggers, Land, Sink, Grab, trace: trace);
                        if (fires && trace is not null)
                        {
                            _harpoon?.Fire(Apex(trace) + new Vector2(0f, _space.Limits.Height * 0.5f), _space, reachedAnchors, anchorQueue);
                        }

                        if (leap && leaps is not null && trace is not null && (move.DashTick < 0 || trace.DashedAt >= 0))
                        {
                            leaps(new Leap(ledge, at, move with { DashTick = trace.DashedAt }, trace.Path, trace.Lands,
                                trace is { Lands: >= 0, Touched: false, Wet: false, Burned: false, Assisted: false }));
                        }
                    }
                }
            }
        }

        return (reachedTriggers, reachedLedges);
    }

    /// <summary>
    /// The moves tried from every take-off point of a ledge, each way (−1, 0, +1): a jump held to the apex or cut after 6 or 12 ticks,
    /// with no air dash or one 6, 14, 22 or 30 ticks in; or walking off the end (a jump of 0 ticks: the sim starts with vy = 0 and
    /// gravity takes over past the edge; tried only outward from a ledge's ends where no floor goes on level, and not from a float line),
    /// with no air dash or one 4, 12 or 20 ticks in.
    /// </summary>
    public static readonly IReadOnlyList<GroundMove> GroundMoves =
    (
        from dir in new[] { -1, 0, 1 }
        from move in
            (from jump in new[] { -1, 6, 12 } from dash in new[] { -1, 6, 14, 22, 30 } select new GroundMove(dir, jump, dash))
            .Concat(dir == 0 ? Enumerable.Empty<GroundMove>() : new[] { -1, 4, 12, 20 }.Select(dash => new GroundMove(dir, 0, dash)))
        select move
    ).ToList();

    /// <summary>The move the Harpoon also fires from, at its top: a full jump straight up, no air dash.</summary>
    private static readonly GroundMove StraightUp = new(0, -1, -1);

    /// <summary>The highest feet of a flight the body was whole at (not the tick it burned).</summary>
    private static Vector2 Apex(Trace trace)
    {
        Vector2 top = trace.Path[0];
        for (int i = 1; i < trace.Path.Count - (trace.Burned ? 1 : 0); i++)
        {
            if (trace.Path[i].Y > top.Y)
            {
                top = trace.Path[i];
            }
        }

        return top;
    }

    /// <summary>What one flight met, for a <see cref="Leap"/>: the feet tick by tick and the ledge it ended on (−1: none).</summary>
    private sealed class Trace
    {
        public List<Vector2> Path { get; } = new();
        public int Lands { get; set; } = -1;
        public int DashedAt { get; set; } = -1;
        /// <summary>A wall or a ceiling met (not the landing).</summary>
        public bool Touched { get; set; }
        public bool Wet { get; set; }
        public bool Burned { get; set; }
        public bool Assisted { get; set; }
    }

    /// <summary>Standing on a lift's platform anywhere along <paramref name="on"/>, carried up the whole of its rise.</summary>
    private void Ride(LiftPath lift, Ledge on, IReadOnlyList<Box> triggers, HashSet<int> reachedTriggers)
    {
        float a = MathF.Max(on.MinX, lift.Platform.MinX), b = MathF.Min(on.MaxX, lift.Platform.MaxX);
        for (float x = a; x <= b + 0.001f; x += MathF.Max(0.25f, (b - a) / 8f))
        {
            for (float up = 0f; up <= lift.Rise + 0.001f; up += 0.25f)
            {
                _space.Touch(new Vector2(x, on.Y + MathF.Min(up, lift.Rise)), triggers, reachedTriggers);
            }

            _space.Touch(new Vector2(x, on.Y + lift.Rise), triggers, reachedTriggers);
        }
    }

    /// <summary>Where on a ledge to take off: its ends (or <see cref="MoveLimits.EdgeMargin"/> inside them), and every 2 m between.</summary>
    private IEnumerable<float> TakeOffs(Ledge l)
    {
        float a = l.MinX, b = l.MaxX;
        if (b - a > 2f * _space.Limits.EdgeMargin)
        {
            a += _space.Limits.EdgeMargin;
            b -= _space.Limits.EdgeMargin;
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
        HashSet<int> reachedTriggers, Action<int> land, Action<Vector2> sink, Action<GripPose> grab, bool airborne = false,
        int gripFrom = 0, Trace? trace = null)
    {
        MoveLimits m = _space.Limits;
        Vector2 p = start, v = velocity;
        trace?.Path.Add(start);
        bool jumping = jumpTicks != 0, cut = false;
        Box? swimming = null;
        airborne |= jumping;
        if (jumping)
        {
            v.Y = m.TakeOff;
        }

        int dashLeft = 0, dashDir = dir == 0 ? 1 : dir;
        bool dashed = false;
        int limit = MaxTicks;
        for (int tick = 0; tick < limit; tick++)
        {
            bool held = jumpTicks < 0 || tick < jumpTicks;
            if (swimming is null && v.Y <= 0f && _water is not null && _water.WaterAt(p) is { } water
                && WaterMotion.Deep(p.Y, water.MaxY, _water.Limits))
            {
                if (_water.Ballast)
                {
                    sink(p);
                    return;
                }

                swimming = water;
                if (trace is not null)
                {
                    trace.Wet = true;
                }

                limit = tick + MaxSwimTicks; // floating up from deep water takes a while
                dashLeft = 0;
                v.Y = WaterMotion.Splash(v.Y, _water.Limits);
            }

            if (swimming is { } w && _water is not null)
            {
                if (_water.WaterAt(p) is null)
                {
                    swimming = null; // swum out of its side
                }
                else
                {
                    if (_water.Swim(_space, _ledges, ref p, ref v, dir, w, triggers, reachedTriggers, land))
                    {
                        return;
                    }

                    continue;
                }
            }

            if (dashTick >= 0 && tick == dashTick && !dashed && m.AirDashes > 0 && airborne)
            {
                dashed = true;
                dashLeft = (int)MathF.Round(m.DashDuration / Tick);
                if (trace is not null)
                {
                    trace.DashedAt = tick;
                }
            }

            if (dashLeft > 0)
            {
                v = new Vector2(dashDir * m.DashDistance / m.DashDuration, 0f);
                if (--dashLeft == 0)
                {
                    v = new Vector2(dashDir * m.RunSpeed * m.DashExitSpeedFactor, 0f);
                }
            }
            else
            {
                if (jumping && !cut && !held && v.Y > 0f)
                {
                    v.Y *= m.JumpCutFactor;
                    cut = true;
                }

                v.X = MoveRules.Air(v.X, dir, m.RunSpeed, m.AirCarrySpeed, m.AirAcceleration, m.AirDeceleration, m.AirDrag, Tick);
                float g = m.Gravity;
                if (jumping && tick == 0)
                {
                    g = 0f; // the take-off tick moves at the whole take-off speed, as the pilgrim's (JumpState.Enter, after gravity)
                }
                else if (held && jumping && MathF.Abs(v.Y) < m.ApexHangThreshold)
                {
                    g *= m.ApexGravityMultiplier;
                }
                else if (v.Y < 0f)
                {
                    g *= m.FallGravityMultiplier;
                }

                v.Y = MathF.Max(v.Y - g * Tick, -m.MaxFallSpeed);
            }

            // Move X, then Y, stopping at faces.
            bool grips = _grip is not null && dashLeft == 0 && tick >= gripFrom;
            float pushX = v.X;
            p.X = _space.MoveAxis(p, v.X * Tick, horizontal: true, out bool hitX, out int solidX);
            if (hitX && dashLeft == 0 && dir != 0 && MathF.Sign(pushX) == dir && airborne && _space.LedgeAssist(p, dir, _space.Solids[solidX]) is { } lifted)
            {
                p = lifted;
                v = new Vector2(dir * TheDeep.LedgeAssist.Speed(pushX, m.RunSpeed), 0f);
                hitX = false;
                if (trace is not null)
                {
                    trace.Assisted = true;
                }
            }

            if (hitX)
            {
                v.X = 0f;
                if (trace is not null)
                {
                    trace.Touched = true;
                }

                if (grips && dir != 0 && MathF.Sign(pushX) == dir && _grip is not null && _grip.IsMetal(solidX))
                {
                    Box wall = _space.Solids[solidX];
                    grab(GripMath.OntoWall(p, m.Height * 0.5f, dir > 0 ? wall.MinX : wall.MaxX, new Vector2(-dir, 0f)));
                }
            }

            float pushY = v.Y;
            p.Y = _space.MoveAxis(p, v.Y * Tick, horizontal: false, out bool hitY, out int solidY);
            if (hitY && grips && pushY > 0f && _grip is not null && _grip.IsMetal(solidY))
            {
                grab(GripMath.OntoCeiling(p, _space.Solids[solidY].MinY));
            }

            if (!hitY || v.Y >= 0f)
            {
                airborne = true; // off the floor (standing, the feet ride a skin over it: their height alone can't tell)
            }

            trace?.Path.Add(p);
            _space.Touch(p, triggers, reachedTriggers);
            if (_space.InHazard(p))
            {
                if (trace is not null)
                {
                    trace.Burned = true;
                }

                return;
            }

            if (hitY && v.Y < 0f)
            {
                // On a floor: a landing after a flight ends the trajectory; before take-off (walking to an edge) it goes on.
                int landed = _space.LedgeAt(_ledges, p);
                land(landed);
                if (airborne || dir == 0 || hitX)
                {
                    if (trace is not null)
                    {
                        trace.Lands = landed;
                    }

                    return;
                }

                v.Y = 0f;
            }
            else if (hitY)
            {
                v.Y = 0f;
                if (trace is not null)
                {
                    trace.Touched = true;
                }
            }
        }
    }

    /// <summary>Whether another floor than <paramref name="l"/>, at its height, holds feet at <paramref name="x"/>.</summary>
    private bool GoesOn(Ledge l, float x) => _ledges.Any(o => o != l && !o.Swim && MathF.Abs(o.Y - l.Y) < 0.01f && x >= o.MinX && x <= o.MaxX);
}
