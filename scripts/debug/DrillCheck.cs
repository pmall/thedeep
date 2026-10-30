using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted Drill check in the mechanics lab (`-- --room MechanicsLab --drill-check`, run by tools/check.sh), through the
/// real input actions, against cracked blocks it places itself: without the Drill a dash stops at cracked rock and leaves
/// it whole; with it the dash bores through and carries on; down and dash on cracked rock dives through it to the floor
/// below; down and dash in the air dives and stops on sound rock (unbroken); down and dash on sound rock is a dash. Each
/// step has a time limit. Prints `DRILL OK` or `DRILL CHECK` errors.
/// </summary>
public partial class DrillCheck : Node
{
    public Player Player { get; set; } = null!;
    public World World { get; set; } = null!;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private bool _dove;
    private Block _wall = null!;
    private Block _slab = null!;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done, bool Hold = false);

    public DrillCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private float X => Player.GlobalPosition.X;
    private float Y => Player.GlobalPosition.Y;

    public override void _Ready()
    {
        // A cracked wall across the floor, and a cracked slab in the air over it.
        _wall = Cracked("CrackedWall", new Vector3(13f, 1.5f, 0f), new Vector3(1f, 3f, 4f));
        _slab = Cracked("CrackedSlab", new Vector3(20f, 3.25f, 0f), new Vector3(4f, 0.5f, 4f));

        _steps.Add(new("without the Drill, a dash stops at cracked rock and leaves it whole", 0.6f,
            () => { Player.Abilities.Lock(Ability.Drill); Place(10.5f, 0f); Input.ActionPress(InputActions.Dash); },
            () => _wall.Broken || X > 12.5f, Hold: true));
        _steps.Add(new("with it, the dash bores through and carries on", 0.5f,
            () => { Input.ActionRelease(InputActions.Dash); Player.Abilities.Unlock(Ability.Drill); Place(10.5f, 0f); Press(InputActions.Dash); },
            () => _wall.Broken && X > 14.5f && Player.CurrentState == Player.States.Dash));
        _steps.Add(new("down and dash on cracked rock: the dive breaks it and lands on the floor below", 1f,
            () => { Release(InputActions.Dash); Place(20f, 3.5f); Input.ActionPress(InputActions.MoveDown); Press(InputActions.Dash); },
            () => _slab.Broken && _dove && Player.IsOnFloor() && Y < 0.05f && Player.CurrentState != Player.States.Dive));
        _steps.Add(new("down and dash in the air: a dive, stopped by sound rock", 1.2f,
            () => { Release(InputActions.Dash); _dove = false; Place(6f, 3f); Press(InputActions.Dash); },
            () => _dove && Player.IsOnFloor() && Y < 0.05f && Y > -0.05f && Player.CurrentState != Player.States.Dive));
        _steps.Add(new("down and dash on sound rock: a dash", 0.3f,
            () => { Release(InputActions.Dash); _dove = false; Place(3f, 0f); Press(InputActions.Dash); },
            () => Player.CurrentState == Player.States.Dash && !_dove));
        Next();
    }

    private Block Cracked(string name, Vector3 center, Vector3 size)
    {
        var block = GD.Load<PackedScene>("res://scenes/kit/Block.tscn").Instantiate<Block>();
        block.Name = name;
        block.Size = size;
        block.Cracked = true;
        block.Position = center;
        World.Room.AddChild(block);
        return block;
    }

    private void Place(float x, float y) => Player.Respawn(new Vector3(x, y, 0f), 1);

    private static void Press(StringName action) => Input.ActionPress(action);

    private static void Release(StringName action) => Input.ActionRelease(action);

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

        _dove |= Player.CurrentState == Player.States.Dive;
        Step step = _steps[_index];
        bool timeUp = ++_ticks > step.Seconds * 60f;
        if (step.Hold)
        {
            // A step that must NOT happen for its whole time.
            if (step.Done())
            {
                Fail(step);
                return;
            }

            if (timeUp)
            {
                Next();
            }

            return;
        }

        if (step.Done())
        {
            Next();
        }
        else if (timeUp)
        {
            Fail(step);
        }
    }

    private void Fail(Step step)
    {
        _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, state {Player.CurrentState.Name}, " +
                      $"wall {(_wall.Broken ? "broken" : "whole")}, slab {(_slab.Broken ? "broken" : "whole")}, dove {_dove})");
        Finish();
    }

    private void Finish()
    {
        _index = _steps.Count;
        foreach (StringName action in new[] { InputActions.Dash, InputActions.MoveDown })
        {
            Input.ActionRelease(action);
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"DRILL CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"DRILL OK: {_steps.Count} steps (stopped without it, bored through, dove through, dove onto sound rock, a dash on sound rock)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
