using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted Harpoon check in the mechanics lab (`-- --room MechanicsLab --harpoon-check`, run by tools/check.sh),
/// through the real input actions: no anchor is in sight without the ability; with it, the near anchor is; firing
/// pulls the pilgrim to it and lets go rising, the air dash back; firing again chains to the next anchor (the one
/// just left is skipped); an anchor behind a solid or out of range is never in sight; a jump during the pull
/// cancels into a jump. Each step has a time limit. Prints `HARPOON OK` or `HARPOON CHECK` errors.
/// </summary>
public partial class HarpoonCheck : Node
{
    public Player Player { get; set; } = null!;
    public World World { get; set; } = null!;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private float _maxAbsZ;
    private float _closest = float.MaxValue;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done, bool Hold = false);

    private HarpoonAnchor A => World.Room.All<HarpoonAnchor>().First(a => a.Name == "AnchorA");

    private HarpoonAnchor B => World.Room.All<HarpoonAnchor>().First(a => a.Name == "AnchorB");

    public HarpoonCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _Ready()
    {
        _steps.Add(new("no anchor in sight without the Harpoon", 0.3f,
            () => { Player.Abilities.Lock(Ability.Harpoon); Place(11f, 0f); },
            () => Player.AnchorInSight is not null, Hold: true));
        _steps.Add(new("with it, the near anchor is in sight", 0.3f,
            () => Player.Abilities.Unlock(Ability.Harpoon),
            () => Player.AnchorInSight == A));
        _steps.Add(new("fire: the line flies out and the pull begins", 0.3f,
            () => Press(InputActions.Harpoon),
            () => Player.CurrentState == Player.States.Harpoon && Player.States.Harpoon.Bitten));
        _steps.Add(new("reach the anchor, let go rising with the air dash back", 1.5f,
            () => { Release(InputActions.Harpoon); _closest = float.MaxValue; },
            () => Player.LastAnchor == A && Player.CurrentState == Player.States.Fall && Player.Velocity.Y > 0f
                  && Player.AirDashesLeft == Player.Tuning.AirDashes && _closest < 0.3f));
        _steps.Add(new("the next anchor is in sight (the one just left is skipped)", 0.2f,
            () => { },
            () => Player.AnchorInSight == B));
        _steps.Add(new("fire again: chain to the next anchor", 1.5f,
            () => Press(InputActions.Harpoon),
            () => Player.LastAnchor == B));
        _steps.Add(new("an anchor behind a solid is not in sight", 0.3f,
            () => { Release(InputActions.Harpoon); Place(14f, 13f); },
            () => Player.AnchorInSight is not null, Hold: true));
        _steps.Add(new("an anchor out of range is not in sight", 0.3f,
            () => Place(2f, 0f),
            () => Player.AnchorInSight is not null, Hold: true));
        _steps.Add(new("fire, and jump during the pull: a jump", 1f,
            () => { Place(11f, 0f); Press(InputActions.Harpoon); },
            () => Player.CurrentState == Player.States.Harpoon && Player.States.Harpoon.Bitten));
        _steps.Add(new("…cancels the pull into a jump", 0.3f,
            () => { Release(InputActions.Harpoon); Press(InputActions.Jump); },
            () => Player.CurrentState == Player.States.Jump));
        Next();
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

        _maxAbsZ = Mathf.Max(_maxAbsZ, Mathf.Abs(Player.GlobalPosition.Z));
        if (Player.CurrentState == Player.States.Harpoon)
        {
            _closest = Mathf.Min(_closest, (Player.States.Harpoon.Anchor.Point - Player.Center).Length());
        }

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
                      $"in sight {Player.AnchorInSight?.Name ?? "none"}, last {Player.LastAnchor?.Name ?? "none"}, closest {_closest:0.00} m)");
        Finish();
    }

    private void Finish()
    {
        _index = _steps.Count;
        foreach (StringName action in new[] { InputActions.Harpoon, InputActions.Jump })
        {
            Input.ActionRelease(action);
        }

        if (_maxAbsZ > 0.001f)
        {
            _problems.Add($"left the play plane (|Z| up to {_maxAbsZ})");
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"HARPOON CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"HARPOON OK: {_steps.Count} steps (sight, fire, pull, release, chain, blocked, range, jump cancel)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
