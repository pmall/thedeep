using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Every burn of the world held in play (`-- --burn-probe`, run by tools/check.sh for both worlds; alone:
/// `godot --headless --path . --fixed-fps 60 --quit-after 20000 -- --burn-probe 2>&1 | grep BURN`), through the real input actions,
/// in every room holding a <see cref="SlagBody"/> or a <see cref="HeatColumn"/>. Its cases come from the room's data, never from
/// coordinates: from each side of each burn, on the lowest open ground beside it with a clear way in at that height (a bank no
/// higher than <see cref="Lip"/> over a slag's surface, any ground beside a heat column within its height), the pilgrim, on foot
/// with no other ability, walks in. Into slag, plated and not, and into heat unplated: it is burned once, set back on rock beside
/// the burn, in control within <see cref="ControlLoss"/> of the blow, and not burned again while it stands there
/// (`docs/gameplay.md#slag`, `docs/gameplay.md#heat`). Into heat plated, it stands in the column unharmed. A death, a burn
/// that never comes, a set-back that never comes or leaving the room is an error. A side with no such ground (a column walled in a
/// shaft, a mould entered from above) is listed, not failed: the room check holds those shapes (<see cref="SlagBasin"/>,
/// <see cref="HeatGate"/>). Prints `BURN CHECK` errors, or `BURN OK` and the sides it could not walk in from.
/// </summary>
public partial class BurnProbe : Node
{
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }

    /// <summary>How far from a burn's edge the pilgrim starts, meters: clear of every keep-off margin, so it is footing.</summary>
    private const float Offset = 1.6f;

    /// <summary>The highest a bank's top stands over a slag's surface and is still walked off into it, meters.</summary>
    private const float Lip = 8f;

    /// <summary>Ticks the pilgrim stands, set back and in control, before its set-back counts as held.</summary>
    private const int Settle = 45;

    /// <summary>The longest a burn may keep control away, from the blow to control regained on rock, ticks (0.45 s).</summary>
    private const int ControlLoss = 27;

    /// <summary>Ticks a case may run before it counts as unfinished.</summary>
    private const int Timeout = 300;

    /// <summary>One walk into a burn: the room, what it is, where it starts and toward which side, plated, whether it must burn, the burn.</summary>
    private sealed record Case(string Room, string What, Vector3 Start, int Dir, bool Plated, bool Burns, Box Region);

    private readonly List<string> _rooms = new();
    private readonly List<Case> _cases = new();
    private readonly List<string> _problems = new();
    private readonly List<string> _unprobed = new();
    private int _roomIndex = -1, _caseIndex, _ticks, _hurts, _hurtAt, _setBackAt, _regainedAt, _inside, _walked, _bodies;
    private bool _placing, _died;

    /// <summary>Runs before the pilgrim each physics tick, so what it presses is read the same tick.</summary>
    public BurnProbe()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private Case Current => _cases[_caseIndex];

    private string At => $"({Player.GlobalPosition.X:0.##}, {Player.GlobalPosition.Y:0.##})";

    public override void _Ready()
    {
        Player.Hurt += () =>
        {
            if (_hurts++ == 0)
            {
                _hurtAt = _ticks;
            }
        };
        Player.Died += () => _died = true;
        Player.Respawned += () =>
        {
            if (!_placing)
            {
                _setBackAt = _ticks;
            }
        };
        foreach (Ability ability in new[] { Ability.MagGrip, Ability.Harpoon, Ability.Drill, Ability.Ballast })
        {
            Player.Abilities.Lock(ability); // on foot, as any pilgrim first meets a burn
        }

        foreach (string path in World.RoomScenes())
        {
            var room = World.Scene(path).Instantiate<Room>();
            if (room.All<SlagBody>().Any() || room.All<HeatColumn>().Any())
            {
                _rooms.Add(path);
            }

            room.Free();
        }

        NextRoom();
    }

    /// <summary>Loads the next room holding a burn and derives its cases from its slag bodies and heat columns.</summary>
    private void NextRoom()
    {
        _cases.Clear();
        _caseIndex = 0;
        while (_cases.Count == 0)
        {
            if (++_roomIndex >= _rooms.Count)
            {
                Finish();
                return;
            }

            Room room = World.Load(World.Scene(_rooms[_roomIndex]));
            string name = World.RoomName;
            var solids = room.Blocks().Select(b => Box.Around(b.GlobalPosition.Flat(), new System.Numerics.Vector2(b.Size.X, b.Size.Y))).ToList();
            foreach (SlagBody slag in room.All<SlagBody>())
            {
                _bodies++;
                Box s = slag.PlayRegion;
                foreach ((string side, int dir, Vector3 start) in Sides(s, s.MaxY, s.MaxY + Lip, solids, $"{name}/{slag.Name}"))
                {
                    foreach (bool plated in new[] { false, true })
                    {
                        _cases.Add(new(name, $"walking into slag {slag.Name} from its {side} bank, {(plated ? "plated" : "unplated")}", start, dir, plated, true, s));
                    }
                }
            }

            foreach (HeatColumn heat in room.All<HeatColumn>())
            {
                _bodies++;
                Box h = heat.Region;
                foreach ((string side, int dir, Vector3 start) in Sides(h, h.MinY, h.MaxY, solids, $"{name}/{heat.Name}"))
                {
                    foreach (bool plated in new[] { false, true })
                    {
                        _cases.Add(new(name, $"walking into heat {heat.Name} from its {side}, {(plated ? "plated" : "unplated")}", start, dir, plated, !plated, h));
                    }
                }
            }
        }

        Start();
    }

    /// <summary>
    /// Where the pilgrim starts on each side of <paramref name="region"/>: <see cref="Offset"/> out, on the lowest solid top
    /// between <paramref name="low"/> and <paramref name="high"/> where its body stands clear and walks in clear at that height.
    /// A side with none is listed as unprobed.
    /// </summary>
    private IEnumerable<(string Side, int Dir, Vector3 Start)> Sides(Box region, float low, float high, List<Box> solids, string what)
    {
        float half = 0.5f * Player.Tuning.Move.Width, height = Player.Tuning.Move.Height;
        foreach ((string side, int dir) in new[] { ("west", 1), ("east", -1) })
        {
            float x = dir > 0 ? region.MinX - Offset : region.MaxX + Offset;
            float edge = dir > 0 ? region.MinX : region.MaxX;
            float? top = solids.Where(s => s.MinX <= x && x <= s.MaxX && s.MaxY >= low - 0.01f && s.MaxY <= high).Select(s => s.MaxY).OrderBy(t => t)
                .Cast<float?>().FirstOrDefault(t => !solids.Any(new Box(Math.Min(x - half, edge), t!.Value + 0.05f, Math.Max(x + half, edge), t.Value + height).Overlaps));
            if (top is { } y)
            {
                _walked++;
                yield return (side, dir, new Vector3(x, y, 0f));
            }
            else
            {
                _unprobed.Add($"{what} {side}");
            }
        }
    }

    private void Start()
    {
        Hold(0);
        _ticks = _hurts = _inside = 0;
        _hurtAt = _setBackAt = _regainedAt = -1;
        Player.Abilities.Set(Ability.HeatPlating, Current.Plated);
        Player.Vitality.Restore();
        _placing = true;
        Player.Respawn(Current.Start, Current.Dir);
        _placing = false;
        Player.Velocity = Vector3.Zero;
    }

    private void Next()
    {
        if (++_caseIndex >= _cases.Count)
        {
            NextRoom();
        }
        else
        {
            Start();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_roomIndex < 0 || _roomIndex >= _rooms.Count)
        {
            return;
        }

        _ticks++;
        Case c = Current;
        if (_died || World.RoomName != c.Room)
        {
            Problem(_died ? $"died at {At}" : $"left the room for {World.RoomName}");
            Finish(); // the session takes the pilgrim elsewhere: what follows would not be the probe's
            return;
        }

        if (c.Burns ? Burned(c) : Passed(c))
        {
            Next();
        }
        else if (_ticks > Timeout)
        {
            Problem($"unfinished after {Timeout / 60f:0.#} s at {At}, burned {_hurts} times, set back {_setBackAt >= 0}, in control {Player.InControl}");
            Next();
        }
    }

    /// <summary>Walking in until burned, then held: set back on rock beside the burn, in control soon, not burned again (true once judged).</summary>
    private bool Burned(Case c)
    {
        Hold(_hurts == 0 ? c.Dir : 0);
        if (_setBackAt < 0)
        {
            return false;
        }

        if (_regainedAt < 0)
        {
            if (!Player.InControl)
            {
                return false;
            }

            _regainedAt = _ticks;
            if (_regainedAt - _hurtAt > ControlLoss)
            {
                Problem($"control gone {(_regainedAt - _hurtAt) / 60f:0.##} s after the burn (at most {ControlLoss / 60f:0.##})");
            }
        }

        if (_ticks - _regainedAt < Settle)
        {
            return false;
        }

        if (_hurts > 1)
        {
            Problem($"burned {_hurts} times: set back at {At}, where it burns again");
        }

        float x = Player.GlobalPosition.X;
        if (!Player.IsOnFloor() || Player.BodyBox.Overlaps(c.Region) || (c.Region.MinX <= x && x <= c.Region.MaxX && Player.GlobalPosition.Y < c.Region.MaxY + 0.5f))
        {
            Problem($"set back to {At}, not standing on rock beside the burn");
        }

        return true;
    }

    /// <summary>Walking in plated: true once the body has stood in the heat for a third of a second, unharmed.</summary>
    private bool Passed(Case c)
    {
        Hold(c.Dir);
        if (_hurts > 0)
        {
            Problem($"burned at {At} though plated");
            return true;
        }

        _inside = Player.BodyBox.Overlaps(c.Region) ? _inside + 1 : 0;
        return _inside >= 20;
    }

    private static void Hold(int dir)
    {
        Input.ActionRelease(dir > 0 ? InputActions.MoveLeft : InputActions.MoveRight);
        if (dir == 0)
        {
            Input.ActionRelease(InputActions.MoveLeft);
        }
        else
        {
            Input.ActionPress(dir > 0 ? InputActions.MoveRight : InputActions.MoveLeft);
        }
    }

    private void Problem(string what) => _problems.Add($"{Current.Room}: {Current.What} (from ({Current.Start.X:0.##}, {Current.Start.Y:0.##})): {what}");

    private void Finish()
    {
        _roomIndex = _rooms.Count;
        Hold(0);
        foreach (string problem in _problems)
        {
            GD.PushError($"BURN CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"BURN OK: {_bodies} slag bodies and heat columns in {_rooms.Count} rooms, walked into from {_walked} sides; " +
                     $"not walked into (no open ground beside): {(_unprobed.Count == 0 ? "none" : string.Join(", ", _unprobed))}");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
