using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The fight moveset in the mechanics lab (`-- --room MechanicsLab --fight-check`, run by tools/check.sh), through the real input
/// actions, on the lab's Votary (renewed between steps, the pilgrim kept whole): presses in rhythm chain a swing, a second and the
/// finisher; a landed blow holds the pilgrim still a moment (the hit stop); a strike in the air holding down is a down strike, and
/// landing it bounces the pilgrim up; a strike during a dash is a dash strike, and it lands. Each step has a time limit. Prints
/// `FIGHT OK` or `FIGHT CHECK` errors.
/// </summary>
public partial class FightCheck : Node
{
    public required Player Player { get; init; }
    public required World World { get; init; }

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private readonly List<StrikeKind> _kinds = new();
    private int _index = -1;
    private int _ticks;
    private bool _stopped, _bounced, _landed;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done, Action? Tick = null);

    public FightCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private Machine Votary => World.Room.First<Machine>() ?? throw new InvalidOperationException("no Machine in the room");

    private static void Hold(StringName action, bool held)
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

    /// <summary>Renews the Votary and clears what the last step saw.</summary>
    private void Renew()
    {
        Votary.Reset();
        _kinds.Clear();
        _stopped = _bounced = _landed = false;
    }

    public override void _Ready()
    {
        if (World.Room.All<Machine>().Count == 0)
        {
            _problems.Add("the lab has no machine to strike");
            Finish();
            return;
        }

        Player.Attack.Swung += strike => _kinds.Add(strike.Kind);
        float x = Votary.PlanePosition.X;
        _steps.Add(new("presses in rhythm chain a swing, a second and the finisher, and a landed blow holds still a moment", 3f,
            () => { Renew(); Player.Respawn(new Vector3(x - 1.4f, 0f, 0f), 1); },
            () => _kinds.Take(3).SequenceEqual(new[] { StrikeKind.Swing, StrikeKind.Second, StrikeKind.Finisher }) && _stopped,
            () => Hold(InputActions.Attack, _ticks % 21 is 1 or 2)));
        _steps.Add(new("in the air holding down, a down strike; landed, it bounces the pilgrim up", 3f,
            () =>
            {
                Input.ActionRelease(InputActions.Attack);
                Renew();
                Player.Respawn(new Vector3(x, 2.6f, 0f), 1);
                Input.ActionPress(InputActions.MoveDown);
            },
            () => _kinds.Contains(StrikeKind.Down) && _bounced,
            () => Hold(InputActions.Attack, _ticks % 21 is 4 or 5)));
        _steps.Add(new("a strike during a dash is a dash strike, and it lands", 3f,
            () =>
            {
                Input.ActionRelease(InputActions.Attack);
                Input.ActionRelease(InputActions.MoveDown);
                Renew();
                Player.Respawn(new Vector3(x + 4.5f, 0f, 0f), -1); // from behind it: a praying Votary strikes whoever is in front
            },
            () => _kinds.Contains(StrikeKind.Dash) && _landed,
            () =>
            {
                Hold(InputActions.Dash, _ticks is 10 or 11);
                Hold(InputActions.Attack, _ticks is 13 or 14);
            }));
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

        Player.Vitality.Restore();
        _stopped |= Player.HitStopped;
        _landed |= Player.HitStopped && Player.Attack.Strike.Kind == StrikeKind.Dash;
        _bounced |= _kinds.Contains(StrikeKind.Down) && Player.Velocity.Y > 5f && !Player.IsOnFloor();
        Step step = _steps[_index];
        _ticks++;
        step.Tick?.Invoke();
        if (step.Done())
        {
            Next();
        }
        else if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, velocity " +
                          $"{Player.Velocity}, strikes {string.Join(",", _kinds)}, hit stop seen {_stopped}, Votary at {Votary.PlanePosition})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        foreach (StringName action in new[] { InputActions.Attack, InputActions.MoveDown, InputActions.Dash })
        {
            Input.ActionRelease(action);
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"FIGHT CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"FIGHT OK: {_steps.Count} steps (a chain to the finisher with a hit stop, a down strike's bounce, a dash strike)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
