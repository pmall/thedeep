using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted check of the flooded layer's end (`-- --room Chapterhouse --encounter-check`, run by tools/check.sh), through the
/// real input actions. In the Chapterhouse: stepping into the arena drops both gates; the Precentor strikes the pilgrim
/// lingering before it; swings break it; the gates rise and the fight is remembered; entering again, it stays broken and
/// the gates stay up. At the Brink: the third law holds the pilgrim at the lip; pressing on breaks it (remembered); over
/// the lip, the jump falls on into Rimefall and lands there. In the Choir: walking past a sleeping Chorister wakes none;
/// a hard landing beside one wakes it. In the Counterweight: standing in the cage lets go of its brake, and it carries the
/// pilgrim (standing on its floor all the way) up into the Undercroft, onto the grate. In the Calving: Mag-grip climbs the
/// girder up the ice cliff and the cliff's top leads back into the Coldstacks. In the Bore: the shaft drops into the
/// chamber, the relic gives the Drill, a dash bores the cracked wall (remembered), the tunnel's steps climb back to the
/// hall. In the Sounding: the drop into the well, floating to the shelf, the relic gives Ballast; sunk under the well's
/// wall, rising in the shaft beyond, out onto the east hall. In the Mere: sunk through a lead to the lake bed and down its
/// shaft into the Underice; the tanks blown, back up into the Mere. In the Coldstacks: a dive through the ice plug drops
/// into the Vault's pool; out of it and into the arena, both gates fall; the Archivist dives at the pilgrim below it; swings
/// break it while it hangs low; the gates rise, remembered. In the Sanctum: the Hierarch's order is heard coming near; the
/// second law holds the pilgrim at the forbidden door; pressing on breaks it (remembered). Prints `ENCOUNTER OK` or
/// `ENCOUNTER CHECK` errors.
/// </summary>
public partial class EncounterCheck : Node
{
    public required Player Player { get; init; }
    public required World World { get; init; }
    public required Session Session { get; init; }

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done, Action? Tick = null);

    public EncounterCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private BossSeal Seal => World.Room.All<BossSeal>().First();
    private Machine Precentor => World.Room.All<Machine>().First(m => m.Species.Id == "precentor");
    private LawBreak Law => World.Room.All<LawBreak>().First();
    private IEnumerable<Machine> Choristers => World.Room.All<Machine>().Where(m => m.Species.Id == "chorister");
    private Lift Cage => World.Room.All<Lift>().First();
    private Machine Archivist => World.Room.All<Machine>().First(m => m.Species.Id == "archivist");
    private bool _ordered;
    private float X => Player.GlobalPosition.X;
    private float _rodeTo;
    private bool _rideProblem;
    private int _jumpHeld;
    private bool _rose;

    private void Hold(StringName action, bool held)
    {
        if (held)
        {
            Input.ActionPress(action);
        }
        else
        {
            Input.ActionRelease(action);
        }
    }

    public override void _Ready()
    {
        _steps.Add(new("walk into the arena: both gates fall", 4f,
            () => { Player.Respawn(new Vector3(3f, 0f, 0f), 1); Hold(InputActions.MoveRight, true); },
            () => Seal.Fighting && Seal.Gates.All(g => g.Shut),
            () => Hold(InputActions.MoveRight, X < 12f)));
        _steps.Add(new("the Precentor strikes the pilgrim lingering before it", 8f,
            () => { Hold(InputActions.MoveRight, false); Player.Respawn(new Vector3(26.5f, 0f, 0f), 1); },
            () => Player.Vitality.Current < Player.Vitality.Max));
        _steps.Add(new("swings break the Precentor", 40f,
            () => { },
            () => Precentor.Broken,
            () =>
            {
                // Kept alive (the check is about the fight's rules, not its balance), close to it and facing it.
                Player.Vitality.Restore();
                float gap = Precentor.PlanePosition.X - X;
                Hold(InputActions.MoveRight, gap > 1.4f);
                Hold(InputActions.MoveLeft, gap < -1.4f);
                if (Mathf.Abs(gap) <= 1.4f && Math.Sign(gap) != Player.WorldFacing && gap != 0f)
                {
                    Hold(gap > 0f ? InputActions.MoveRight : InputActions.MoveLeft, true);
                }

                Hold(InputActions.Attack, _ticks % 21 is 1 or 2);
            }));
        _steps.Add(new("the gates rise and the fight is remembered", 4f,
            () => { Hold(InputActions.MoveRight, false); Hold(InputActions.MoveLeft, false); Hold(InputActions.Attack, false); },
            () => Seal.Gates.All(g => g.Raised) && Session.Save.Collected.Contains("Chapterhouse/Fight")));
        _steps.Add(new("entering again: the Precentor stays broken, the gates stay up", 3f,
            () =>
            {
                World.Load(GD.Load<PackedScene>(World.RoomPath("Chapterhouse")));
                Player.Respawn(new Vector3(3f, 0f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => _ticks > 120 && X > 12f && Precentor.Broken && Seal.Gates.All(g => g.Raised) && !Seal.Fighting,
            () => Hold(InputActions.MoveRight, X < 14f)));
        _steps.Add(new("at the Brink, the third law holds the pilgrim at the lip", 2f,
            () =>
            {
                Hold(InputActions.MoveRight, false);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Brink")));
                Player.Respawn(new Vector3(18f, 0f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => _ticks >= 75 && X < 24f && Player.IsOnFloor() && !Law.Broken && Law.Strain > 0.1f));
        _steps.Add(new("pressing on breaks it, and it is remembered", 5f,
            () => { },
            () => Law.Broken && Session.Save.Collected.Contains("Brink/ThirdLaw")));
        _steps.Add(new("over the lip, the jump falls on into Rimefall and lands on its shelf", 12f,
            () => { },
            () => World.RoomName == "Rimefall" && Player.InControl && Player.IsOnFloor() && Player.GlobalPosition.Y > 9f,
            () => Hold(InputActions.MoveRight, World.RoomName == "Brink" && Player.GlobalPosition.Y > -2f)));
        _steps.Add(new("in the Choir, walking past a sleeping Chorister wakes none", 4f,
            () =>
            {
                Hold(InputActions.MoveRight, false);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Choir")));
                Player.Respawn(new Vector3(13f, -2f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => X > 20f && Choristers.All(c => c.Brain.Act == MachineAct.Sleep),
            () => Hold(InputActions.MoveRight, X < 20.5f)));
        _steps.Add(new("a hard landing beside one wakes it", 3f,
            () => { Hold(InputActions.MoveRight, false); Player.Respawn(new Vector3(24f, 3f, 0f), 1); },
            () => Choristers.Single(c => c.Name == "Chorister1").Brain.Act != MachineAct.Sleep
                  && Choristers.Single(c => c.Name == "Chorister0").Brain.Act == MachineAct.Sleep));
        _steps.Add(new("in the Counterweight, standing in the cage lets go of its brake", 4f,
            () =>
            {
                World.Load(GD.Load<PackedScene>(World.RoomPath("Counterweight")));
                Player.Respawn(new Vector3(8f, 0f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => Cage.Travel.Phase == LiftPhase.Rising,
            () => Hold(InputActions.MoveRight, X < 12.6f)));
        _steps.Add(new("it carries the pilgrim up the shaft, through the Undercroft's floor, onto the grate", 16f,
            () => { _rodeTo = 0f; },
            () => World.RoomName == "Undercroft" && Player.InControl && Player.IsOnFloor() && Mathf.Abs(X - 42f) < 2.1f
                  && Mathf.Abs(Player.GlobalPosition.Y) < 0.05f && _rodeTo > 25f,
            () =>
            {
                if (World.RoomName != "Counterweight")
                {
                    return;
                }

                // Riding, not left behind: the pilgrim's feet stay on the cage floor all the way up.
                float above = Player.GlobalPosition.Y - Cage.Cage.GlobalPosition.Y;
                _rodeTo = Mathf.Max(_rodeTo, Player.GlobalPosition.Y);
                if (Mathf.Abs(above) > 0.25f && !_rideProblem)
                {
                    _rideProblem = true;
                    _problems.Add($"the pilgrim left the cage floor on the way up ({above:0.00} m from it, the cage {Cage.Travel.Offset:0.0} m up)");
                }
            }));
        _steps.Add(new("in the Calving, the girder climbs the ice cliff back to the Coldstacks' doorway (Mag-grip)", 10f,
            () =>
            {
                World.Load(GD.Load<PackedScene>(World.RoomPath("Calving")));
                Player.Abilities.Unlock(Ability.MagGrip);
                Player.Respawn(new Vector3(25f, 0f, 0f), 1);
                Hold(InputActions.MoveRight, true);
            },
            () => World.RoomName == "Coldstacks",
            () =>
            {
                // Into the girder, up it, then east along the cliff's top to the doorway.
                bool climbing = Player.CurrentState == Player.States.Grip;
                Hold(InputActions.MoveUp, climbing);
                Hold(InputActions.MoveRight, !climbing || Player.GlobalPosition.Y > 15.5f);
            }));
        _steps.Add(new("in the Bore, the shaft drops into the chamber and the relic gives the Drill", 10f,
            () =>
            {
                Hold(InputActions.MoveUp, false);
                Hold(InputActions.MoveRight, false);
                Player.Abilities.Lock(Ability.MagGrip);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Bore")));
                Player.Respawn(new Vector3(22f, 0f, 0f), -1);
                Hold(InputActions.MoveLeft, true);
            },
            () => Player.Abilities.Has(Ability.Drill) && Player.InControl,
            () => Hold(InputActions.MoveLeft, Player.InControl && X > 4.5f)));
        _steps.Add(new("a dash bores through the cracked wall, remembered; the tunnel's steps climb back to the hall", 10f,
            () => { Hold(InputActions.MoveLeft, false); Hold(InputActions.MoveRight, true); },
            () => X > 43f && X < 46f && Player.IsOnFloor() && Mathf.Abs(Player.GlobalPosition.Y) < 0.05f
                  && Session.Save.Collected.Contains("Bore/CrackedWall"),
            () =>
            {
                Hold(InputActions.Dash, X > 16.5f && X < 19f && _ticks % 20 < 2);
                // Full jumps up each step: pressed on the floor, held a while.
                if (X > 26f && X < 41f && Player.IsOnFloor() && _jumpHeld <= 0)
                {
                    _jumpHeld = 26;
                }

                Hold(InputActions.Jump, --_jumpHeld > 0);
            }));
        _steps.Add(new("in the Sounding, the drop into the well, floating to the shelf: the relic gives Ballast", 10f,
            () =>
            {
                Hold(InputActions.Jump, false);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Sounding")));
                Player.Respawn(new Vector3(10f, 0f, 0f), 1);
            },
            () => Player.Abilities.Has(Ability.Ballast) && Player.InControl,
            () =>
            {
                // Over the lip, then floating: a full jump onto the shelf at the waterline.
                bool floating = Player.CurrentState == Player.States.Swim && Player.Velocity.Y > -0.5f;
                Hold(InputActions.MoveRight, X < 20.5f);
                if (floating && _jumpHeld <= -10)
                {
                    _jumpHeld = 26;
                }

                Hold(InputActions.Jump, --_jumpHeld > 0);
            }));
        _steps.Add(new("sunk under the well's wall and up the shaft beyond, out onto the east hall", 12f,
            () => { Hold(InputActions.Jump, false); Hold(InputActions.MoveRight, false); _jumpHeld = 0; },
            () => X > 34.5f && Player.IsOnFloor() && Player.GlobalPosition.Y > -4.1f && Player.CurrentState != Player.States.Swim,
            () =>
            {
                // Off the shelf into the water, down to the floor, east under the wall; in the shaft, blow the tanks, rise, jump out.
                bool beyond = X > 30.5f, onShelf = Player.IsOnFloor() && Player.CurrentState != Player.States.Swim && X < 30f;
                Hold(InputActions.MoveLeft, onShelf);
                Hold(InputActions.MoveDown, !beyond && !onShelf);
                Hold(InputActions.MoveUp, beyond && Player.Sunk);
                Hold(InputActions.MoveRight, !onShelf);
                if (beyond && !Player.Sunk && Player.CurrentState == Player.States.Swim && Player.Velocity.Y > -0.5f && _jumpHeld <= -10)
                {
                    _jumpHeld = 26;
                }

                Hold(InputActions.Jump, --_jumpHeld > 0);
            }));
        _steps.Add(new("in the Mere, sunk through a lead to the lake bed and down its shaft into the Underice", 12f,
            () =>
            {
                Hold(InputActions.Jump, false);
                Hold(InputActions.MoveUp, false);
                Hold(InputActions.MoveLeft, false);
                Hold(InputActions.MoveRight, false);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Mere")));
                Player.Respawn(new Vector3(21.5f, 1f, 0f), 1);
            },
            () => World.RoomName == "Underice",
            () =>
            {
                Hold(InputActions.MoveDown, true);
                Hold(InputActions.MoveRight, X < 31.5f);
            }));
        _steps.Add(new("in the Underice, sunk clear of the shaft, then the tanks blown: up the shaft, back into the Mere", 10f,
            () => Hold(InputActions.MoveRight, false),
            () => World.RoomName == "Mere" && Player.InControl,
            () =>
            {
                bool clear = World.RoomName == "Underice" && Player.InControl && Player.GlobalPosition.Y < 19f;
                _rose |= clear;
                Hold(InputActions.MoveDown, !_rose);
                Hold(InputActions.MoveUp, _rose);
            }));
        _steps.Add(new("in the Coldstacks, down and dash on the ice plug: the dive breaks it and drops into the Vault's pool", 8f,
            () =>
            {
                Hold(InputActions.MoveUp, false);
                Player.Abilities.Unlock(Ability.Drill);
                World.Load(GD.Load<PackedScene>(World.RoomPath("Coldstacks")));
                Player.Respawn(new Vector3(64f, -10f, 0f), 1);
                Hold(InputActions.MoveDown, true);
            },
            () => World.RoomName == "Vault" && Player.InControl && Player.CurrentState == Player.States.Swim,
            () => Hold(InputActions.Dash, World.RoomName == "Coldstacks" && _ticks is > 10 and < 14)));
        _steps.Add(new("out of the pool and west into the arena: both gates fall", 8f,
            () => { Hold(InputActions.MoveDown, false); Hold(InputActions.Dash, false); Hold(InputActions.MoveLeft, true); _jumpHeld = 0; },
            () => Seal.Fighting && Seal.Gates.All(g => g.Shut),
            () =>
            {
                if (Player.CurrentState == Player.States.Swim && Player.Velocity.Y > -0.5f && _jumpHeld <= -10)
                {
                    _jumpHeld = 26;
                }

                Hold(InputActions.Jump, --_jumpHeld > 0);
                Hold(InputActions.MoveLeft, X > 30f);
            }));
        _steps.Add(new("the Archivist dives at the pilgrim lingering below it", 12f,
            () => { Hold(InputActions.Jump, false); Hold(InputActions.MoveLeft, false); },
            () => Player.Vitality.Current < Player.Vitality.Max));
        _steps.Add(new("swings break the Archivist while it hangs after its dives", 90f,
            () => { },
            () => Archivist.Broken,
            () =>
            {
                // Kept alive (the rules, not the balance); under it while it hangs low, swinging, jumping to reach it.
                Player.Vitality.Restore();
                Vector2 at = Archivist.PlanePosition;
                float gap = at.X - X, rise = at.Y - Player.GlobalPosition.Y;
                bool low = Archivist.Intent.Act is MachineAct.Recover or MachineAct.Strike;
                Hold(InputActions.MoveRight, low && gap > 1.2f);
                Hold(InputActions.MoveLeft, low && gap < -1.2f);
                if (Mathf.Abs(gap) <= 1.2f && Math.Sign(gap) != Player.WorldFacing && gap != 0f)
                {
                    Hold(gap > 0f ? InputActions.MoveRight : InputActions.MoveLeft, true);
                }

                Hold(InputActions.Jump, low && rise > 2.2f && Mathf.Abs(gap) < 2.5f && Player.IsOnFloor() && _ticks % 30 < 14);
                Hold(InputActions.Attack, Mathf.Abs(gap) < 2.6f && _ticks % 21 is 1 or 2);
            }));
        _steps.Add(new("the Vault's gates rise and the fight is remembered", 4f,
            () =>
            {
                foreach (StringName action in new[] { InputActions.MoveRight, InputActions.MoveLeft, InputActions.Attack, InputActions.Jump })
                {
                    Hold(action, false);
                }
            },
            () => Seal.Gates.All(g => g.Raised) && Session.Save.Collected.Contains("Vault/Arena")));
        _steps.Add(new("in the Sanctum, coming near the Hierarch: its order is heard", 6f,
            () =>
            {
                Session.Spoke += OnSpoke;
                World.Load(GD.Load<PackedScene>(World.RoomPath("Sanctum")));
                Player.Respawn(new Vector3(40f, 0f, 0f), -1);
                Hold(InputActions.MoveLeft, true);
            },
            () => _ordered));
        _steps.Add(new("the second law holds the pilgrim at the forbidden door", 3f,
            () => { },
            () => _ticks >= 90 && X > 7f && Player.IsOnFloor() && !Law.Broken && Law.Strain > 0.1f));
        _steps.Add(new("pressing on breaks it, and it is remembered; past it, the end of what is built", 6f,
            () => { },
            () => Law.Broken && Session.Save.Collected.Contains("Sanctum/Law") && X < 6f));
        Next();
    }

    private void OnSpoke(Proclamation proclamation) => _ordered |= proclamation.Name == "Order";

    private void Next()
    {
        _index++;
        _ticks = 0;
        if (_index >= _steps.Count)
        {
            Finish();
            return;
        }

        _steps[_index].Start();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_index < 0 || _index >= _steps.Count)
        {
            return;
        }

        Step step = _steps[_index];
        _ticks++;
        step.Tick?.Invoke();
        if (step.Done())
        {
            Next();
            return;
        }

        if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" did not happen in {step.Seconds} s (in {World.RoomName} at {Player.GlobalPosition}, " +
                          $"health {Player.Vitality.Current}/{Player.Vitality.Max}, state {Player.CurrentState.Name})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        foreach (StringName action in new[] { InputActions.Attack, InputActions.MoveRight, InputActions.MoveLeft, InputActions.MoveUp,
                     InputActions.MoveDown, InputActions.Dash, InputActions.Jump })
        {
            Input.ActionRelease(action);
        }

        Session.Spoke -= OnSpoke;
        foreach (string problem in _problems)
        {
            GD.PushError($"ENCOUNTER CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"ENCOUNTER OK: {_steps.Count} steps (arena gates, the Precentor struck and broken, remembered; the third law held, broken, the fall into Rimefall; the Choir asleep, woken; the Counterweight's ride up; the Calving's girder; the Bore's relic, its cracked wall, its steps; the Sounding's relic, under its wall; the Mere's lake bed down into the Underice and back; the ice plug into the Vault, its gates, the Archivist broken, remembered; the Hierarch's order, the second law held, broken)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
