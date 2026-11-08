using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The Sacristy's hidden passage in place (`-- --room Sacristy --secret-check`, run by tools/check.sh), through the real input
/// actions: the false wall stands and its way is hidden on the map; walking west into it finds it (it fades, remembered); walking on
/// crosses into the Cell; walking back, the wall is gone for good. Each step has a time limit. Prints `SECRET OK` or `SECRET CHECK`
/// errors.
/// </summary>
public partial class SecretCheck : Node
{
    public required Player Player { get; init; }
    public required World World { get; init; }
    public required Secrets Secrets { get; init; }
    public required Session Session { get; init; }

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private bool _found;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    public SecretCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private FalseWall? Wall => World.Room.First<FalseWall>();

    public override void _Ready()
    {
        Secrets.Found += _ => _found = true;
        _steps.Add(new("the false wall stands, and the way behind it is hidden on the map", 0.5f,
            () => { },
            () => Wall is { Found: false } && World.Map.WaysOf("Sacristy").Any(w => w.Hidden && w.Exit == "West")));
        _steps.Add(new("walking west into it finds it", 3f,
            () => { Player.Respawn(new Vector3(5f, 0f, 0f), -1); Input.ActionPress(InputActions.MoveLeft); },
            () => _found && Wall is { Found: true } && Session.Save.Collected.Contains($"Sacristy/{Wall.Name}")));
        _steps.Add(new("walking on crosses into the Cell", 3f, () => { }, () => World.RoomName == "Cell" && Player.InControl));
        _steps.Add(new("walking back, the wall is gone for good", 5f,
            () => { Input.ActionRelease(InputActions.MoveLeft); Input.ActionPress(InputActions.MoveRight); },
            () => World.RoomName == "Sacristy" && Wall is { Found: true }));
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
        if (step.Done())
        {
            Next();
        }
        else if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (in {World.RoomName} at {Player.GlobalPosition}, " +
                          $"found {_found})");
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
            GD.PushError($"SECRET CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"SECRET OK: {_steps.Count} steps (a false wall and a hidden way, found by walking in, through to the Cell, gone for good)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
