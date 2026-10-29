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
/// a hard landing beside one wakes it. Prints `ENCOUNTER OK` or `ENCOUNTER CHECK` errors.
/// </summary>
public partial class EncounterCheck : Node
{
    public Player Player { get; set; } = null!;
    public World World { get; set; } = null!;
    public Session Session { get; set; } = null!;

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
    private float X => Player.GlobalPosition.X;

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
        Next();
    }

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
        foreach (StringName action in new[] { InputActions.Attack, InputActions.MoveRight, InputActions.MoveLeft })
        {
            Input.ActionRelease(action);
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"ENCOUNTER CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"ENCOUNTER OK: {_steps.Count} steps (arena gates, the Precentor struck and broken, remembered; the third law held, broken, the fall into Rimefall; the Choir asleep, woken)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
