using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted water and Ballast check in the mechanics lab (`-- --room MechanicsLab --ballast-check`, run by tools/check.sh), through
/// the real input actions, in a tank of water it places itself: dropped in, the pilgrim splashes and settles floating on its float
/// line; without Ballast, holding down keeps it floating; with Ballast, holding down sinks it to the bottom, where it stays when let
/// go; up blows the tanks and it rises back to float; a jump near the float line leaves the water. Each step has a time limit.
/// Prints `BALLAST OK` or `BALLAST CHECK` errors.
/// </summary>
public partial class BallastCheck : Node
{
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }
    /// <summary>The running game's gate hints.</summary>
    public required GateHints Hints { get; init; }

    /// <summary>The gates the pilgrim has said it can't pass (GateHints).</summary>
    private readonly System.Collections.Generic.HashSet<Ability> _hinted = new();

    /// <summary>The tank: its surface, its middle, its floor (the lab's).</summary>
    private const float Surface = 5f, Middle = 12.5f, Bottom = 0f;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private float _lowest;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done, bool Hold = false);

    /// <summary>Runs before the pilgrim each frame and each physics tick, so what it presses is read the same tick.</summary>
    public BallastCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private float Y => Player.GlobalPosition.Y;
    private float Line => WaterMotion.FloatLine(Surface, Player.Water);
    private bool Swimming => Player.CurrentState == Player.States.Swim;

    public override void _Ready()
    {
        World.Room.AddChild(new WaterBody
        {
            Name = "Tank",
            Swim = true,
            Size = new Vector2(4f, 4f),
            Depth = Surface - Bottom,
            Position = new Vector3(Middle, Surface, 0f),
            SurfaceMaterial = GD.Load<Material>("res://assets/materials/water.tres"),
        });

        Hints.Said += kind => _hinted.Add(kind);
        _steps.Add(new("dropped in, it splashes and settles floating on the float line", 3f,
            () => { Player.Abilities.Lock(Ability.Ballast); Player.Respawn(new Vector3(Middle, Surface + 4f, 0f), 1); },
            () => Swimming && Mathf.Abs(Y - Line) < 0.1f && Mathf.Abs(Player.Velocity.Y) < 0.3f));
        _steps.Add(new("without Ballast, holding down keeps it floating", 1.5f,
            () => { _lowest = Y; Input.ActionPress(InputActions.MoveDown); },
            () => !Swimming || Y < Line - 0.5f || Player.Sunk, Hold: true));
        _steps.Add(new("and the pilgrim says it is too light to sink (the gate hint)", 1f,
            () => { },
            () => _hinted.Contains(Ability.Ballast)));
        _steps.Add(new("with Ballast, holding down sinks it to the bottom", 3f,
            () => Player.Abilities.Unlock(Ability.Ballast),
            () => Swimming && Player.Sunk && Player.IsOnFloor() && Y < Bottom + 0.05f));
        _steps.Add(new("let go, it stays on the bottom", 1f,
            () => Input.ActionRelease(InputActions.MoveDown),
            () => !Player.Sunk || Y > Bottom + 0.05f, Hold: true));
        _steps.Add(new("up blows the tanks: it rises back to float", 3f,
            () => Input.ActionPress(InputActions.MoveUp),
            () => Swimming && !Player.Sunk && Mathf.Abs(Y - Line) < 0.15f));
        _steps.Add(new("a jump near the float line leaves the water", 1f,
            () => { Input.ActionRelease(InputActions.MoveUp); Input.ActionPress(InputActions.Jump); },
            () => Y > Surface + 0.3f && !Swimming));
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

        _lowest = Mathf.Min(_lowest, Y);
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
        _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, velocity {Player.Velocity}, " +
                      $"state {Player.CurrentState.Name}, sunk {Player.Sunk}, water {Player.WaterSurface?.ToString(CultureInfo.InvariantCulture) ?? "none"}, float line {Line})");
        Finish();
    }

    private void Finish()
    {
        _index = _steps.Count;
        foreach (StringName action in new[] { InputActions.MoveDown, InputActions.MoveUp, InputActions.Jump })
        {
            Input.ActionRelease(action);
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"BALLAST CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"BALLAST OK: {_steps.Count} steps (splashed in and floated, held up without it, the gate hint, sank, stayed down, rose, jumped out)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
