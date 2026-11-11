using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The Slag held in play (`-- --room Slag --slag-check`, run by tools/check.sh; alone:
/// `godot --headless --path . --fixed-fps 60 --quit-after 15000 -- --room Slag --slag-check | grep SLAG`), through the real input
/// actions, on the abilities any pilgrim first walks it with: its routes run west to east and back, plated and not (a jump at
/// every gap and every step up, a step down walked off), the climb up the mound to the vent, a jump straight up on every
/// walkway, standing still at every take-off edge, walking off into each slag body, plated and not, the way out of the coolant
/// pool, the drop down the Drill's shaft and the runs over the plug. Every tick it records each snag: the head meeting a ceiling,
/// the floor lost for a few ticks on flat ground, the body jolted up or down while it stands, a burn while standing on rock,
/// standing alive on slag, a death, stopping against a wall it can't jump, leaving the room; a route that doesn't finish is one
/// too, and so is a fall into slag that doesn't end set back on rock, in control and unburned there (`docs/gameplay.md#slag`).
/// Prints `SLAG CHECK` errors, one per snag, or `SLAG OK`.
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

    /// <summary>The deepest drop the runner walks off rather than jumps, meters (a top level with slag is no step: it jumps over).</summary>
    private const float StepDown = 3f;

    /// <summary>Ticks stopped against a wall, after jumping at it, that make the runner blocked.</summary>
    private const int Stuck = 30;

    /// <summary>Ticks the pilgrim stands still after a burn sets it back, unburned, before a fall counts as ended.</summary>
    private const int Settle = 45;

    /// <summary>A snag: the route it happened on, its kind, where, and what.</summary>
    private sealed record Snag(int Route, string Kind, Vector2 At, string What);

    /// <summary>A route: what it does, its time limit, how it starts and drives (true once done).</summary>
    private sealed record Route(string What, float Seconds, Action Start, Func<bool> Drive);

    private readonly List<Route> _routes = new();
    private readonly List<Snag> _snags = new();
    private List<Box> _solids = new();
    private List<Box> _slag = new();
    private int _index = -1, _ticks, _clock, _jumpTick = -100, _stillTicks, _hurts;
    private bool _jumpHeld, _wasOnFloor, _placing, _dying, _setBack;
    private int _respawnedAt = -1, _airFrom = -1, _diedAt;
    private float _airY, _takeoffY;

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
        _routes.Add(new("the climb from the shrine up the mound's steps to the vent", 10f, () => Place(new(38.75f, -1.5f), 1, false),
            () => Run(1, true, Player.IsOnFloor() && P.Y > 5.9f)));
        // A jump straight up on every walkway (the mound's top step is under the vent, the way out).
        foreach ((float x, float y) in new[] { (3f, 0f), (7.5f, -2f), (10.5f, -4.5f), (16.5f, -6.5f), (24.5f, -5f), (32f, -3.5f), (38.75f, -1.5f),
                     (41f, 1f), (51f, 1f), (43.5f, 3.5f), (48.5f, 3.5f), (53.5f, -1.5f), (60.5f, -1.5f), (66f, 0f), (88f, 0f) })
        {
            _routes.Add(new($"a jump straight up at ({x}, {y})", 3f, () => Place(new(x, y), 1, false), () =>
            {
                if (_ticks == 5)
                {
                    Jump();
                }

                return _ticks > 20 && Player.IsOnFloor();
            }));
        }

        // Standing still at every take-off edge over slag: no burn reaches there.
        foreach ((float x, float y, int facing) in new[] { (17.8f, -6.5f, 1), (23.2f, -5f, -1), (25.8f, -5f, 1), (30.7f, -3.5f, -1), (33.3f, -3.5f, 1),
                     (37.7f, -1.5f, -1), (54.3f, -1.5f, 1), (58.2f, -1.5f, -1) })
        {
            _routes.Add(new($"standing still at the edge ({x}, {y})", 1.5f, () => Place(new(x, y), facing, false), () => _ticks > 85));
        }

        // Walking off into each slag body: burned and set back on rock, plated or not, never killed.
        foreach (bool plated in new[] { false, true })
        {
            foreach ((float edge, float top, int dir) in new[] { (18f, -6.5f, 1), (26f, -5f, 1), (33.5f, -3.5f, 1), (54.5f, -1.5f, 1), (58f, -1.5f, -1) })
            {
                _routes.Add(new($"walking off the edge at x {edge} into the slag, {(plated ? "plated" : "unplated")}", 6f,
                    () => Place(new(edge - dir * 1.2f, top), dir, plated), () => Fall(dir)));
            }
        }

        _routes.Add(new("out of the coolant pool, floating up and jumping east onto its wall", 8f, () => Place(new(13.5f, -12f), 1, false), Swim));
        _routes.Add(new("falling down the Drill's shaft (x 60..63), walled from the slag", 3f, () => Place(new(61.5f, -4f), 0, false), () => P.Y < -14f));
        _routes.Add(new("running east from the mound over the plug onto the east floor", 4f, () => Place(new(52.5f, -1.5f), 1, false),
            () => Run(1, true, Player.IsOnFloor() && P.X > 66f)));
        _routes.Add(new("running west from the east floor over the plug onto the mound", 4f, () => Place(new(66f, 0f), -1, false),
            () => Run(-1, true, Player.IsOnFloor() && P.X < 53f && P.Y > -2f)));
        Next();
    }

    private void Main(string what, bool plated, Vector2 from, int dir, float toX) =>
        _routes.Add(new(what, 40f, () => Place(from, dir, plated), () => Run(dir, true, dir * (P.X - toX) > 0 && Player.IsOnFloor())));

    /// <summary>
    /// Walking off an edge toward <paramref name="dir"/> into the slag: done once burned, set back on rock and standing there in
    /// control for <see cref="Settle"/> ticks; a death, a second burn there or a fall that never burns is a snag.
    /// </summary>
    private bool Fall(int dir)
    {
        Hold(_hurts == 0 && !_dying ? dir : 0);
        if (_dying)
        {
            return true;
        }

        if (_setBack && Player.InControl && _clock - _respawnedAt > Settle)
        {
            if (_hurts > 1)
            {
                Record("burned again", $"burned {_hurts} times: set back where the slag burns again, at ({P.X:0.##}, {P.Y:0.##})");
            }

            if (!Player.IsOnFloor() || _slag.Any(s => P.Y < s.MaxY + 0.5f && s.MinX - 0.5f < P.X && P.X < s.MaxX + 0.5f))
            {
                Record("bad footing", $"set back to ({P.X:0.##}, {P.Y:0.##}), not standing on rock clear of the slag");
            }

            return true;
        }

        if (_ticks == (int)(_routes[_index].Seconds * 60f) - 1 && !_setBack)
        {
            Record("no burn", $"fell into the slag and was neither burned and set back nor dead, at ({P.X:0.##}, {P.Y:0.##})");
        }

        return false;
    }

    /// <summary>Floating up the pool and leaving it east: jump pressed every few ticks, done standing on the pool's wall.</summary>
    private bool Swim()
    {
        Hold(1);
        if (_ticks % 20 == 0)
        {
            Input.ActionPress(InputActions.Jump);
        }
        else if (_ticks % 20 == 6)
        {
            Input.ActionRelease(InputActions.Jump);
        }

        return Player.IsOnFloor() && P.Y > -7f && P.X > 15f;
    }

    /// <summary>
    /// Runs toward <paramref name="dir"/>, jumping (held, a full jump) over a gap ahead (a step down under <see cref="StepDown"/>
    /// is walked off) and at a step up ahead, when <paramref name="jumps"/>. Stopped on flat ground against a wall for
    /// <see cref="Stuck"/> ticks, it is blocked (a snag).
    /// </summary>
    private bool Run(int dir, bool jumps, bool arrived)
    {
        Hold(arrived ? 0 : dir);
        float ahead = P.X + dir * 0.3f;
        bool gap = !Solid(ahead, P.Y - 0.1f) && !_solids.Any(s => s.MinX <= ahead && ahead <= s.MaxX && s.MaxY < P.Y && s.MaxY > P.Y - StepDown);
        bool wall = Solid(P.X + dir * 0.6f, P.Y + 0.7f);
        _stillTicks = !arrived && Player.IsOnFloor() && Math.Abs(Player.Velocity.X) < 0.05f ? _stillTicks + 1 : 0;
        if (_stillTicks >= Stuck)
        {
            Record("blocked", $"running {(dir > 0 ? "east" : "west")}, stopped against a wall at ({P.X:0.##}, {P.Y:0.##}) it can't jump");
        }

        if (jumps && !arrived && Player.IsOnFloor() && (gap || wall))
        {
            Jump();
        }

        return arrived;
    }

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
        _hurts++;
        if (_wasOnFloor && !OverSlag())
        {
            Record("burn on rock", $"hurt at ({P.X:0.##}, {P.Y:0.##}) standing on rock, plated {Player.Abilities.Has(Ability.HeatPlating)}");
        }
        else if (!_routes[_index].What.StartsWith("walking off", StringComparison.Ordinal))
        {
            Record("burn", $"hurt at ({P.X:0.##}, {P.Y:0.##}) on a route that never meets the slag");
        }
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
        _hurts = 0;
        _dying = _setBack = false;
        _stillTicks = 0;
        _airFrom = _respawnedAt = -1;
        if (_index >= _routes.Count)
        {
            Finish();
            return;
        }

        _solids = World.Room.Blocks().Select(b => Box.Around(b.GlobalPosition.Flat(), new System.Numerics.Vector2(b.Size.X, b.Size.Y))).ToList();
        _slag = World.Room.All<SlagBody>().Select(s => s.PlayRegion).ToList();
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
        bool done = route.Drive() || _dying;
        if (_dying)
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
            GD.PushError($"SLAG CHECK: {snag.Kind}: route {snag.Route + 1} \"{_routes[Math.Min(snag.Route, _routes.Count - 1)].What}\": {snag.What}");
        }

        if (_snags.Count == 0)
        {
            GD.Print($"SLAG OK: {_routes.Count} routes (on foot both ways, plated or not; the climb to the vent; headroom on every walkway; " +
                     "no burn at a take-off edge; every fall into slag burned and set back on rock; out of the pool; the shaft walled)");
        }

        GetTree().Quit(_snags.Count == 0 ? 0 : 1);
    }
}
