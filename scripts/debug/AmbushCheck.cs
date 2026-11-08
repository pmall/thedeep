using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The Pyre's trap in place (`-- --room Pyre --ambush-check`, run by tools/check.sh), through the real input actions where they
/// matter: before it springs its wave is not there and the gate is up; walking in past the doorway springs it (the gate falls behind,
/// the wave stands up); walking back is barred by the gate; when the wave is broken (struck down directly: the fight itself is the
/// combat check's) the gate rises and the trap is remembered; the room loaded again holds no trap, its gate up. Each step has a time
/// limit. Prints `AMBUSH OK` or `AMBUSH CHECK` errors.
/// </summary>
public partial class AmbushCheck : Node
{
    public required Player Player { get; init; }
    public required World World { get; init; }
    public required Session Session { get; init; }

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private bool _sprang;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    public AmbushCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private Ambush? Trap => World.Room.First<Ambush>();

    public override void _Ready()
    {
        if (Trap is not { } trap)
        {
            _problems.Add("the Pyre has no ambush");
            Finish();
            return;
        }

        trap.Sprang += _ => _sprang = true;
        Portcullis gate = trap.Gates[0];
        float gateX = gate.GlobalPosition.X;
        _steps.Add(new("before it springs, the wave is not there and the gate is up", 0.5f,
            () => { },
            () => trap.Wave.All(m => m.Broken && !m.Visible) && gate.Raised && !trap.Fighting));
        _steps.Add(new("walking in past the doorway springs it: the gate falls behind, the wave stands up", 3f,
            () => { Player.Respawn(new Vector3(gateX - 1.2f, 0f, 0f), 1); Input.ActionPress(InputActions.MoveRight); },
            () => _sprang && trap.Fighting && gate.Shut && trap.Wave.All(m => !m.Broken && m.Visible)));
        _steps.Add(new("walking back, the gate bars the way out", 1.5f,
            () => { Input.ActionRelease(InputActions.MoveRight); Input.ActionPress(InputActions.MoveLeft); },
            () => _ticks > 60 && Player.GlobalPosition.X > gateX && World.RoomName == "Pyre"));
        _steps.Add(new("the wave broken, the gate rises and the trap is remembered", 4f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveLeft);
                foreach (Machine machine in trap.Wave)
                {
                    machine.TakeHit(999, machine.PlanePosition.X - 1f, 0f);
                }
            },
            () => trap.Cleared && gate.Raised && Session.Save.Collected.Contains($"Pyre/{trap.Name}")));
        _steps.Add(new("the room loaded again holds no trap, its gate up", 1f,
            () => World.Load(GD.Load<PackedScene>(World.RoomPath("Pyre"))),
            () => Trap is { Cleared: true } again && again.Gates.All(g => g.Raised) && again.Wave.All(m => !m.Visible)));
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

        Player.Vitality.Restore(); // the trap's rule is checked, not the fight
        Step step = _steps[_index];
        _ticks++;
        if (step.Done())
        {
            Next();
        }
        else if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, sprang {_sprang}, " +
                          $"fighting {Trap?.Fighting}, cleared {Trap?.Cleared})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        Input.ActionRelease(InputActions.MoveLeft);
        Input.ActionRelease(InputActions.MoveRight);
        foreach (string problem in _problems)
        {
            GD.PushError($"AMBUSH CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"AMBUSH OK: {_steps.Count} steps (no wave before, sprung behind a falling gate, the way out barred, cleared and " +
                     "remembered, gone for good)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
