using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The Slag measured in play (`-- --room Slag --slag-check`, run by tools/check.sh; alone:
/// `godot --headless --path . --fixed-fps 60 --quit-after 15000 -- --room Slag --slag-check | grep SLAG`), through the real input
/// actions: its routes walked and jumped west to east and back, plated and not, the jumps under its ledges and the climb up them, the
/// falls between its platforms, standing at its edges and on its slag, the drop down the Drill's shaft and the run over the plug. Every
/// tick it records each snag: the head meeting a ceiling in a jump, the floor lost for a few ticks on flat ground, the body jolted up or
/// down while it stands, a burn while standing on rock, standing alive on slag, a death; a route that doesn't finish is one too. It
/// prints `SLAG` lines: each snag, a verdict per suspect of `docs/roadmap.md#volcanic-rethink`'s measurement ((a) to (f)), then
/// `SLAG REPORT`. Report-only (it never fails) until the Slag is rebuilt on the bricks; then its snags become failures.
/// </summary>
public partial class SlagCheck : Node
{
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }

    /// <summary>Upward or downward speed of a body standing on flat ground that counts as a jolt, m/s.</summary>
    private const float Jolt = 0.5f;

    /// <summary>The longest loss of the floor on flat ground that counts as a drop (longer is a fall), ticks.</summary>
    private const int DropTicks = 4;

    /// <summary>A snag: the route it happened on, its kind, where, and what.</summary>
    private sealed record Snag(int Route, string Kind, Vector2 At, string What);

    /// <summary>A route: its suspect ((a) to (f), or "" for the main routes), what it does, its time limit, how it starts and drives.</summary>
    private sealed record Route(string Suspect, string What, float Seconds, Action Start, Func<bool> Drive);

    private readonly List<Route> _routes = new();
    private readonly List<Snag> _snags = new();
    private readonly List<string> _outcomes = new();
    private List<Box> _solids = new();
    private List<Box> _slag = new();
    private int _index = -1, _ticks, _clock, _jumpTick = -100, _phase;
    private bool _jumpHeld, _wasOnFloor, _placing, _dying, _setBack, _hurtThisRoute;
    private int _respawnedAt = -1, _airFrom = -1, _diedAt;
    private float _airY, _takeoffY;
    private float? _crossedSlab;

    /// <summary>Runs before the pilgrim each physics tick, so what it presses is read the same tick.</summary>
    public SlagCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private Vector2 P => new(Player.GlobalPosition.X, Player.GlobalPosition.Y);

    public override void _Ready()
    {
        Player.Hurt += OnHurt;
        Player.Died += () =>
        {
            _dying = true;
            _diedAt = _clock;
            Record("death", $"died at ({P.X:0.##}, {P.Y:0.##})");
        };
        Player.Respawned += () =>
        {
            if (!_placing)
            {
                _respawnedAt = _clock;
                _setBack = !_dying;
            }
        };
        foreach (Ability ability in new[] { Ability.MagGrip, Ability.Harpoon, Ability.Drill, Ability.Ballast, Ability.HeatPlating })
        {
            Player.Abilities.Lock(ability); // the routes on foot, as any pilgrim first walks them
        }

        Main("west to east on foot, unplated", plated: false, new(3f, 0f), 1, 88f);
        Main("west to east on foot, plated", plated: true, new(3f, 0f), 1, 88f);
        Main("east to west on foot, unplated", plated: false, new(88f, 0f), -1, 4f);
        JumpUnder("a", "a jump straight up under LedgeA", new(41.5f, -2f));
        JumpUnder("a", "a jump straight up under LedgeB (over the shrine)", new(47.5f, -2f));
        _routes.Add(new("a", "the climb from PlatformThree to LedgeA, LedgeB and LedgeC", 15f, () => Place(new(45.2f, -2f), -1, false), Climb));
        foreach (bool plated in new[] { false, true })
        {
            foreach ((float gap, float top) in new[] { (24f, -6.5f), (37f, -4f), (52f, -2f), (68f, -2.5f) })
            {
                _routes.Add(new("d", $"walking off the edge at x {gap} into the river, {(plated ? "plated" : "unplated")}", 6f,
                    () => Place(new(gap - 1.2f, top), 1, plated), () => Fall(gap)));
            }
        }

        foreach (float x in new[] { 23.5f, 23.7f, 23.9f })
        {
            _routes.Add(new("c", $"standing still on PlatformOne at x {x}", 1.5f, () => Place(new(x, -6.5f), 1, false), () => _ticks > 85));
        }

        _routes.Add(new("b", "standing on PoolEast (x 15..17)", 1.5f, () => Place(new(16f, -9f), -1, false), () => _ticks > 85));
        _routes.Add(new("b", "walking west off PlatformOne", 2f, () => Place(new(17.6f, -6.5f), -1, false), () =>
        {
            Hold(_ticks < 12 ? -1 : 0);
            return _ticks > 110;
        }));
        _routes.Add(new("e", "falling down the Drill's shaft (x 60..63) past the river's slab", 3f, () => Place(new(61.5f, -5.5f), 0, false), Shaft));
        _routes.Add(new("f", "running east over PlatformFourWest, the Plug and PlatformFourEast", 4f, () => Place(new(56.6f, -3f), 1, false), () => Run(1, false, P.X > 67.4f)));
        _routes.Add(new("f", "running west back over them", 4f, () => Place(new(67.4f, -2.5f), -1, false), () => Run(-1, false, P.X < 56.6f)));
        Next();
    }

    private void Main(string what, bool plated, Vector2 from, int dir, float toX) =>
        _routes.Add(new("", what, 40f, () => Place(from, dir, plated), () => Run(dir, true, dir * (P.X - toX) > 0 && Player.IsOnFloor())));

    private void JumpUnder(string suspect, string what, Vector2 at) =>
        _routes.Add(new(suspect, what, 3f, () => Place(at, 1, false), () =>
        {
            if (_ticks == 5)
            {
                Jump();
            }

            return _ticks > 20 && Player.IsOnFloor();
        }));

    /// <summary>Up the ledges: onto LedgeA (west), LedgeB (east, jumping at its edge), LedgeC (west, jumping from LedgeB's middle).</summary>
    private bool Climb()
    {
        bool landed = Player.IsOnFloor() && _ticks - _phase > 10;
        switch (_phase == 0 ? 0 : P.Y > 2.8f ? 2 : 1)
        {
            case 0:
                Hold(-1);
                if (_ticks == 3)
                {
                    Jump();
                }

                if (landed && P.Y > 0.3f)
                {
                    _phase = _ticks;
                }

                return false;
            case 1:
                if (Player.IsOnFloor() && P.Y < 0f)
                {
                    Record("unfinished", $"fell from the jump off LedgeA back to ({P.X:0.##}, {P.Y:0.##})");
                    return true;
                }

                return Run(1, true, false);
            default:
                Hold(-1);
                if (P.X < 48.6f && Player.IsOnFloor() && P.Y < 3.3f)
                {
                    Jump();
                }

                return landed && P.Y > 5.3f;
        }
    }

    /// <summary>Walking off the edge at <paramref name="gap"/>: done once dead, or set back by a burn and in control again.</summary>
    private bool Fall(float gap)
    {
        if (!_hurtThisRoute && !_dying)
        {
            Hold(1);
        }
        else
        {
            Hold(0);
        }

        bool done = _dying || (_setBack && _clock - _respawnedAt > 10 && Player.InControl);
        if (!_outcomeTold && (done || _ticks == (int)(_routes[_index].Seconds * 60f)))
        {
            _outcomeTold = true;
            string outcome = _dying ? $"died at ({P.X:0.##}, {P.Y:0.##})"
                : _setBack ? $"scorched, set back to ({P.X:0.##}, {P.Y:0.##})"
                : $"neither burned nor dead, at ({P.X:0.##}, {P.Y:0.##})";
            _outcomes.Add($"{_routes[_index].What}: {outcome}");
        }

        return done;
    }

    /// <summary>The drop down the shaft: whether the body crossed the slab's height there, and how.</summary>
    private bool Shaft()
    {
        foreach (Box s in _slag.Where(s => _crossedSlab is null && s.MinX <= P.X && P.X <= s.MaxX && P.Y < s.MinY))
        {
            _crossedSlab = s.MaxY;
        }

        return P.Y < -12f;
    }

    /// <summary>
    /// Runs toward <paramref name="dir"/>, jumping (held, a full jump) over a gap ahead when <paramref name="jumps"/> (a step down
    /// under <see cref="StepDown"/> is walked off). Stopped on flat ground against a wall for <see cref="Stuck"/> ticks, it is
    /// blocked (a snag), and jumps.
    /// </summary>
    private bool Run(int dir, bool jumps, bool arrived)
    {
        Hold(arrived ? 0 : dir);
        float ahead = P.X + dir * 0.3f;
        bool gap = !Solid(ahead, P.Y - 0.1f) && !_solids.Any(s => s.MinX <= ahead && ahead <= s.MaxX && s.MaxY < P.Y && s.MaxY > P.Y - StepDown &&
                                                                    !_slag.Any(l => Math.Abs(l.MaxY - s.MaxY) < 0.05f && l.MinX < s.MaxX && s.MinX < l.MaxX));
        _stillTicks = !arrived && Player.IsOnFloor() && Math.Abs(Player.Velocity.X) < 0.05f ? _stillTicks + 1 : 0;
        if (_stillTicks >= Stuck)
        {
            Record("blocked", $"running {(dir > 0 ? "east" : "west")}, stopped against a wall at ({P.X:0.##}, {P.Y:0.##}): it takes a jump");
            Jump();
        }
        else if (jumps && !arrived && Player.IsOnFloor() && gap)
        {
            Jump();
        }

        return arrived;
    }

    /// <summary>The deepest drop the runner walks off rather than jumps, meters (a top level with slag is no step: it jumps over).</summary>
    private const float StepDown = 3f;

    /// <summary>Ticks stopped against a wall that make the runner blocked.</summary>
    private const int Stuck = 15;

    private int _stillTicks, _fTicks;

    /// <summary>The fastest vertical speed while standing on the runs over the plug (suspect f), m/s.</summary>
    private float _fMaxSpeed;
    private bool _outcomeTold;

    private bool Solid(float x, float y) => _solids.Any(s => s.MinX <= x && x <= s.MaxX && s.MinY <= y && y <= s.MaxY);

    private static void Hold(int dir)
    {
        if (dir > 0)
        {
            Input.ActionRelease(InputActions.MoveLeft);
            Input.ActionPress(InputActions.MoveRight);
        }
        else if (dir < 0)
        {
            Input.ActionRelease(InputActions.MoveRight);
            Input.ActionPress(InputActions.MoveLeft);
        }
        else
        {
            Input.ActionRelease(InputActions.MoveRight);
            Input.ActionRelease(InputActions.MoveLeft);
        }
    }

    private void Jump()
    {
        if (!_jumpHeld)
        {
            Input.ActionPress(InputActions.Jump);
            _jumpHeld = true;
            _jumpTick = _clock;
            _takeoffY = P.Y;
        }
    }

    private void Place(Vector2 feet, int facing, bool plated)
    {
        Player.Abilities.Set(Ability.HeatPlating, plated);
        Player.Vitality.Restore();
        _placing = true;
        Player.Respawn(new Vector3(feet.X, feet.Y, 0f), facing);
        _placing = false;
        Player.Velocity = Vector3.Zero;
        _takeoffY = feet.Y;
    }

    private void OnHurt()
    {
        _hurtThisRoute = true;
        bool onRock = _wasOnFloor && !OverSlag();
        Record(onRock ? "burn on rock" : "burn", $"hurt at ({P.X:0.##}, {P.Y:0.##}), {(onRock ? "standing on rock" : "in the air or on slag")}, " +
                                                $"plated {Player.Abilities.Has(Ability.HeatPlating)}");
    }

    private bool OverSlag() => _slag.Any(s => s.MinX <= P.X && P.X <= s.MaxX && Math.Abs(P.Y - s.MaxY) < 0.15f);

    private void Record(string kind, string what)
    {
        // One snag of a kind per half meter per route: a contact held for several ticks is one snag.
        if (!_snags.Any(s => s.Route == _index && s.Kind == kind && Math.Abs(s.At.X - P.X) < 0.5f))
        {
            _snags.Add(new Snag(_index, kind, P, what));
        }
    }

    private void Next()
    {
        Hold(0);
        Input.ActionRelease(InputActions.Jump);
        _jumpHeld = false;
        _index++;
        _ticks = 0;
        _phase = 0;
        _dying = _setBack = _hurtThisRoute = _outcomeTold = false;
        _stillTicks = 0;
        _airFrom = _respawnedAt = -1;
        if (_index >= _routes.Count)
        {
            Finish();
            return;
        }

        _solids = World.Room.Blocks().Select(b => Box.Around(b.GlobalPosition.Flat(), new System.Numerics.Vector2(b.Size.X, b.Size.Y))).ToList();
        _slag = World.Room.All<SlagBody>().Select(s => s.PlayRegion).ToList();
        if (World.Room.GetNodeOrNull<MeshInstance3D>("Kit/River") is { } river)
        {
            Aabb box = river.GlobalTransform * river.GetAabb();
            _slag.Add(new Box(box.Position.X, box.Position.Y, box.End.X, box.End.Y));
        }

        _routes[_index].Start();
        _wasOnFloor = Player.IsOnFloor();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_index < 0 || _index >= _routes.Count)
        {
            return;
        }

        _clock++;
        _ticks++;
        if (World.RoomName != "Slag")
        {
            Record("left the room", $"the route took the pilgrim to {World.RoomName}");
            Finish();
            return;
        }

        Watch();
        Route route = _routes[_index];
        bool done = route.Drive();
        if (_dying && route.Suspect != "d")
        {
            done = true;
        }

        if (_dying && done)
        {
            // Wait for the session to bring the pilgrim back from its death before the next route.
            Hold(0);
            if ((_respawnedAt < 0 || _clock - _respawnedAt < 20) && _clock - _diedAt < 600)
            {
                return;
            }
        }

        if (done)
        {
            Next();
        }
        else if (_ticks > route.Seconds * 60f)
        {
            Record("unfinished", $"the route did not finish in {route.Seconds} s: at ({P.X:0.##}, {P.Y:0.##}), velocity {Player.Velocity}, in control {Player.InControl}");
            Next();
        }
    }

    /// <summary>Every tick: the snags of the tick before (the pilgrim moves after this node).</summary>
    private void Watch()
    {
        bool onFloor = Player.IsOnFloor();
        if (_jumpHeld && onFloor && _clock - _jumpTick > 4)
        {
            Input.ActionRelease(InputActions.Jump);
            _jumpHeld = false;
        }

        bool jumping = _clock - _jumpTick < 8;
        if (Player.IsOnCeiling() && Player.InControl)
        {
            Record("ceiling", $"head met a ceiling at ({P.X:0.##}, head {P.Y + Player.Tuning.Move.Height:0.##}) {P.Y - _takeoffY:0.##} m over the take-off " +
                              $"(a full jump rises {Player.Tuning.Move.JumpHeight:0.##})");
        }

        if (_wasOnFloor && !onFloor && !jumping && Player.InControl)
        {
            _airFrom = _clock;
            _airY = P.Y;
        }
        else if (onFloor && _airFrom >= 0)
        {
            if (_clock - _airFrom <= DropTicks && Math.Abs(P.Y - _airY) < 0.1f)
            {
                Record("floor drop", $"the floor lost for {_clock - _airFrom} ticks on flat ground at ({P.X:0.##}, {P.Y:0.##})");
            }

            _airFrom = -1;
        }
        else if (_airFrom >= 0 && _clock - _airFrom > DropTicks)
        {
            _airFrom = -1;
        }

        if (onFloor && _wasOnFloor && !jumping && _routes[_index].Suspect == "f")
        {
            _fTicks++;
            _fMaxSpeed = Math.Max(_fMaxSpeed, Math.Abs(Player.Velocity.Y));
        }

        if (onFloor && _wasOnFloor && !jumping && Math.Abs(Player.Velocity.Y) > Jolt)
        {
            Record("jolt", $"vertical velocity {Player.Velocity.Y:0.##} m/s while standing at ({P.X:0.##}, {P.Y:0.##})");
        }

        if (onFloor && !Player.Vitality.Dead && OverSlag())
        {
            Record("on slag", $"standing alive on slag's surface at ({P.X:0.##}, {P.Y:0.##})");
        }

        _wasOnFloor = onFloor;
    }

    private void Finish()
    {
        _index = _routes.Count;
        Hold(0);
        Input.ActionRelease(InputActions.Jump);
        foreach (Snag snag in _snags)
        {
            GD.Print($"SLAG {snag.Kind}: route {snag.Route + 1} \"{_routes[Math.Min(snag.Route, _routes.Count - 1)].What}\": {snag.What}");
        }

        foreach (string outcome in _outcomes)
        {
            GD.Print($"SLAG fall: {outcome}");
        }

        Verdict("a", "ceilings over walkways", Of("ceiling").Where(s => s.At.X > 39f && s.At.X < 50f));
        Verdict("b", "standing on slag", Of("on slag"));
        Verdict("c", "burned on rock", Of("burn on rock"));
        bool inverted = _outcomes.Any(o => o.Contains("unplated") && o.Contains("set back")) && _outcomes.Any(o => o.Contains(", plated") && o.Contains("died"));
        GD.Print($"SLAG (d) inverted rule: {(inverted ? "CONFIRMED" : "not reproduced")}: " +
                 $"unplated falls {Count("unplated", "set back")} set back, {Count("unplated", "died")} dead; plated falls {Count(", plated", "set back")} set back, {Count(", plated", "died")} dead");
        string shaft = _crossedSlab is { } y
            ? $"the body fell through the slab's top (y {y:0.##}) in the shaft unhindered{(Of("burn").Concat(Of("burn on rock")).Any(s => _routes[s.Route].Suspect == "e") ? ", burned" : ", unburned")}"
            : "the body never crossed the slab in the shaft";
        string planes = string.Join("; ", _slag.SelectMany(s => _solids.Where(b => Math.Abs(b.MaxY - s.MaxY) < 0.01f && b.MinX < s.MaxX && s.MinX < b.MaxX)
            .Select(b => $"slag's top at y {s.MaxY:0.##} level with a block's top at x {b.MinX:0.##}..{b.MaxX:0.##}")));
        GD.Print($"SLAG (e) slab through the shaft: {(_crossedSlab is not null || planes != "" ? "CONFIRMED" : "not reproduced")}: {shaft}; {(planes == "" ? "no shared plane" : planes)}");
        Verdict("f", $"ghost edges and steps over the plug ({_fTicks} ticks standing, vertical speed at most {_fMaxSpeed:0.###} m/s)",
            Of("floor drop").Concat(Of("jolt")).Concat(Of("blocked")).Where(s => _routes[s.Route].Suspect == "f"));
        IEnumerable<Snag> elsewhere = _snags.Where(s => s.Kind is "floor drop" or "jolt" or "unfinished" or "death" && _routes[s.Route].Suspect is "" or "a");
        GD.Print($"SLAG other snags on the main routes and the climb: {elsewhere.Count()} " +
                 $"({string.Join(", ", elsewhere.GroupBy(s => s.Kind).Select(g => $"{g.Key} {g.Count()}"))})");
        GD.Print($"SLAG REPORT (report-only until the Slag is rebuilt, docs/roadmap.md#volcanic-rethink): {_routes.Count} routes, {_snags.Count} snags");
        GetTree().Quit(0);
    }

    private IEnumerable<Snag> Of(string kind) => _snags.Where(s => s.Kind == kind);

    private int Count(string plating, string outcome) => _outcomes.Count(o => o.Contains(plating) && o.Contains(outcome));

    private static void Verdict(string suspect, string what, IEnumerable<Snag> found)
    {
        List<Snag> list = found.ToList();
        GD.Print($"SLAG ({suspect}) {what}: " + (list.Count == 0 ? "not reproduced"
            : $"CONFIRMED, {list.Count}: {string.Join(" | ", list.Take(4).Select(s => s.What))}"));
    }
}
