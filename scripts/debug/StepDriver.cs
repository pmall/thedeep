using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The runner every scripted check shares: a list of timed steps, run one after the other by physics tick. A subclass builds its
/// steps in <see cref="Build"/> (<see cref="Steps"/>), says how to word a failure (<see cref="Detail"/>) and its success
/// (<see cref="Outcome"/>), and may watch the game every tick (<see cref="Observe"/>). A step starts, then must become done
/// within its seconds, or fails the check; a hold step is the reverse: it must stay not done for its whole time. The run ends by
/// releasing every input, printing each problem as a `TAG CHECK:` error or `TAG OK:` and its outcome, and quitting with the
/// verdict (tools/check.sh reads these lines).
/// </summary>
public abstract partial class StepDriver : Node
{
    /// <summary>One thing the check waits for: what it is, the seconds allowed, how it starts, when it is done, what it does each tick
    /// while it runs (before its verdict), and whether it is a hold step (done means failed, the time up means passed).</summary>
    protected sealed record Step(string What, float Seconds, Action Start, Func<bool> Done, Action? Tick = null, bool Hold = false);

    /// <summary>The check's steps, in order: a subclass fills it in <see cref="Build"/>.</summary>
    protected List<Step> Steps { get; } = new();

    private int _index = -1;
    private int _ticks;

    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }

    /// <summary>What the check's problems are, in the order found.</summary>
    protected List<string> Problems { get; } = new();

    /// <summary>The word its result lines start with: `X` in `X OK` and `X CHECK`.</summary>
    protected abstract string Tag { get; }

    /// <summary>Physics ticks the current step has run, counted from 1 on its first tick.</summary>
    protected int Ticks => _ticks;

    /// <summary>How many steps the check has.</summary>
    protected int StepCount => Steps.Count;

    /// <summary>Runs before the pilgrim each frame and each physics tick, so what it presses is read the same tick
    /// (<paramref name="early"/>: false for a check that runs through a paused game instead).</summary>
    protected StepDriver(bool early = true)
    {
        if (early)
        {
            ProcessPriority = -100;
            ProcessPhysicsPriority = -100;
        }
    }

    /// <summary>Lays out the check: its <see cref="Steps"/>, and what it needs listening to. A check that cannot run calls
    /// <see cref="Fail"/> instead.</summary>
    protected abstract void Build();

    /// <summary>What went wrong at the step that failed, for its error line: where the pilgrim was and what the check saw.</summary>
    protected abstract string Detail { get; }

    /// <summary>What a passing run prints after `TAG OK: `.</summary>
    protected abstract string Outcome { get; }

    /// <summary>What a failed step's error line says it did not do, before its seconds.</summary>
    protected virtual string Failed => "failed in";

    /// <summary>What an error line starts with before its step, if anything (the room checked).</summary>
    protected virtual string FailPrefix => "";

    /// <summary>Whether the check ends as soon as it has a problem, rather than running its steps out.</summary>
    protected virtual bool StopAtFirstProblem => false;

    /// <summary>Runs each physics tick the check is running, before the step's own tick and verdict.</summary>
    protected virtual void Observe(float delta)
    {
    }

    /// <summary>Runs when a step is about to start, its ticks counted anew.</summary>
    protected virtual void Entering()
    {
    }

    /// <summary>Runs as the check ends, before its problems are printed: the place to add the problems found only at the end, or
    /// to undo what the check changed.</summary>
    protected virtual void Ending()
    {
    }

    public override void _Ready()
    {
        Build();
        if (_index < 0)
        {
            Next();
        }
    }

    /// <summary>Presses <paramref name="action"/>.</summary>
    protected static void Press(StringName action) => Input.ActionPress(action);

    /// <summary>Releases <paramref name="action"/>.</summary>
    protected static void Release(StringName action) => Input.ActionRelease(action);

    /// <summary>Presses <paramref name="action"/> if <paramref name="held"/>, releases it if not.</summary>
    protected static void Hold(StringName action, bool held) => CheckKit.Hold(action, held);

    /// <summary>Puts the pilgrim at <paramref name="x"/>, <paramref name="y"/> of the play plane, facing east.</summary>
    protected void Place(float x, float y) => Player.Respawn(new Vector3(x, y, 0f), 1);

    /// <summary>Ends the check at once with <paramref name="problem"/>.</summary>
    protected void Fail(string problem)
    {
        Problems.Add(problem);
        Finish();
    }

    /// <summary>Moves on to the next step, or ends the check after the last (or at the first problem, if it asks).</summary>
    protected void Next()
    {
        _index++;
        _ticks = 0;
        if (_index >= Steps.Count || (StopAtFirstProblem && Problems.Count > 0))
        {
            Finish();
            return;
        }

        Entering();
        Steps[_index].Start();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_index < 0 || _index >= Steps.Count)
        {
            return;
        }

        Observe((float)delta);
        Step step = Steps[_index];
        bool timeUp = ++_ticks > step.Seconds * 60f;
        step.Tick?.Invoke();
        if (step.Hold)
        {
            // A step that must NOT happen for its whole time.
            if (step.Done())
            {
                FailStep(step);
            }
            else if (timeUp)
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
            FailStep(step);
        }
    }

    private void FailStep(Step step)
    {
        Problems.Add($"{FailPrefix}step {_index + 1} \"{step.What}\" {Failed} {step.Seconds} s ({Detail})");
        Finish();
    }

    private void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _index = Steps.Count;
        CheckKit.ReleaseAll();
        Ending();
        foreach (string problem in Problems)
        {
            GD.PushError($"{Tag} CHECK: {problem}");
        }

        if (Problems.Count == 0)
        {
            GD.Print($"{Tag} OK: {Outcome}");
        }

        GetTree().Quit(Problems.Count == 0 ? 0 : 1);
    }

    private bool _finished;
}
