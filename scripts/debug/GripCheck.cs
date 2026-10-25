using System;
using System.Collections.Generic;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Scripted Mag-grip check in the mechanics lab (`-- --room MechanicsLab --grip-check`, run by tools/check.sh),
/// through the real input actions: press into a metal wall and stick to it, climb it and turn under the slab
/// above (inner corner), wrap around the slab's end and onto its top (two outer corners, then upright);
/// jump into a metal block's underside and hang from it, walk out and wrap up onto its top; leap off a
/// wall; let go of a wall by pulling away. Each step has a time limit. Prints `GRIP OK` or `GRIP CHECK` errors.
/// </summary>
public partial class GripCheck : Node
{
    public Player Player { get; set; } = null!;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private readonly HashSet<NVector2> _upsSeen = new();
    private int _index = -1;
    private int _ticks;
    private float _maxAbsZ;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    public GripCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _Ready()
    {
        NVector2 left = new(-1f, 0f), right = new(1f, 0f), down = new(0f, -1f);
        _steps.Add(new("stick to the metal wall when pressing into it", 2f,
            () => { Place(20f, 0f); Player.Abilities.Unlock(Ability.MagGrip); Press(InputActions.MoveRight); },
            () => Player.CurrentState == Player.States.Grip && Player.SurfaceUp == left));
        _steps.Add(new("climb the wall, turn under the slab, wrap its end and stand on its top", 9f,
            () => { Release(InputActions.MoveRight); Press(InputActions.MoveUp); },
            () => Player.IsUpright && Player.IsOnFloor() && Player.GlobalPosition.Y > 12.9f));
        _steps.Add(new("seen on the way: the wall, the slab's underside, its end face", 0f, () => { }, () =>
            _upsSeen.Contains(left) && _upsSeen.Contains(down) && _upsSeen.Contains(new NVector2(-1f, 0f))));
        _steps.Add(new("jump into the block's underside and hang from it", 2f,
            () => { Release(InputActions.MoveUp); Place(8f, 0f); Press(InputActions.Jump); },
            () => Player.CurrentState == Player.States.Grip && Player.SurfaceUp == down));
        _steps.Add(new("walk out under the block, wrap its end and stand on its top", 5f,
            () => { Release(InputActions.Jump); Press(InputActions.MoveRight); },
            () => Player.IsUpright && Player.IsOnFloor() && Mathf.Abs(Player.GlobalPosition.Y - 6.5f) < 0.05f));
        _steps.Add(new("stick to the wall again", 2f,
            () => { Place(20f, 0f); Press(InputActions.MoveRight); },
            () => Player.CurrentState == Player.States.Grip));
        _steps.Add(new("leap off the wall, away from it and upright", 1f,
            () => { Release(InputActions.MoveRight); Press(InputActions.Jump); },
            () => Player.IsUpright && Player.Velocity.X < -3f && Player.Velocity.Y > 0f));
        _steps.Add(new("stick to the wall once more", 2.5f,
            () => { Release(InputActions.Jump); Place(20f, 0f); Press(InputActions.MoveRight); },
            () => Player.CurrentState == Player.States.Grip));
        _steps.Add(new("let go by pulling away from the wall", 1f,
            () => { Release(InputActions.MoveRight); Press(InputActions.MoveLeft); },
            () => Player.IsUpright && Player.CurrentState != Player.States.Grip));
        _ = right;
        Next();
    }

    private void Place(float x, float y)
    {
        Player.Respawn(new Vector3(x, y, 0f), 1);
    }

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

        _upsSeen.Add(Player.SurfaceUp);
        _maxAbsZ = Mathf.Max(_maxAbsZ, Mathf.Abs(Player.GlobalPosition.Z));
        Step step = _steps[_index];
        if (step.Done())
        {
            Next();
            return;
        }

        if (++_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" did not happen in {step.Seconds} s (at {Player.GlobalPosition}, " +
                          $"state {Player.CurrentState.Name}, up {Player.SurfaceUp})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        foreach (StringName action in new[] { InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.Jump })
        {
            Input.ActionRelease(action);
        }

        if (_maxAbsZ > 0.001f)
        {
            _problems.Add($"left the play plane (|Z| up to {_maxAbsZ})");
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"GRIP CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"GRIP OK: {_steps.Count} steps (wall, inner corner, outer corners, ceiling, leap, let go)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
