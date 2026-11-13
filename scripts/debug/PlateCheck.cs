using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A scripture plate read in place (`-- --room Sanctum --plate-check`, run by tools/check.sh), through the real input actions: stood
/// at the room's first <see cref="ScripturePlate"/>, "Read" is offered on screen by the pilgrim; interact has the banner show its
/// title and words as the book writes them, and its glyphs glow; they glow as long as the words are on screen, and settle once they
/// have gone. Each step has a time limit. Prints `PLATE OK` or `PLATE CHECK` errors.
/// </summary>
public partial class PlateCheck : Node
{
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }
    /// <summary>The running game's banner.</summary>
    public required Banner Banner { get; init; }

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private bool _dimmed;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    /// <summary>Runs before the session each physics tick, so what it presses is read the same tick.</summary>
    public PlateCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _Ready()
    {
        if (World.Room.First<ScripturePlate>() is not { } plate)
        {
            _problems.Add($"the {World.RoomName} has no scripture plate");
            Finish();
            return;
        }

        string title = LineBook.Shared.Find(plate.TitleLine)?.Text ?? "";
        _steps.Add(new("stood at the plate, \"Read\" is offered on screen by the pilgrim", 3f,
            () => Player.Respawn(plate.GlobalPosition + new Vector3(0.3f, 0.05f, 0f), 1),
            () => plate.Prompt.Offered && DialogueCheck.InSight(plate.Prompt, Player) && plate.Glowing == 0f));
        _steps.Add(new("interact shows its title and words in the banner, and its glyphs glow", 1.5f,
            () => Input.ActionPress(InputActions.Interact),
            () => Banner.Current == plate.Said && Banner.ShownTitle == title && title.Length > 0 && plate.Glowing >= 0.99f));
        _steps.Add(new("they glow as long as the words are on screen", 20f,
            () => Input.ActionRelease(InputActions.Interact),
            () =>
            {
                _dimmed |= Banner.Current == plate.Said && plate.Glowing < 0.99f;
                return !Banner.Showing;
            }));
        _steps.Add(new("and settle once the words have gone", plate.GlowFade + 0.5f,
            () => { },
            () => plate.Glowing == 0f && !_dimmed));
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
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, banner " +
                          $"{Banner.Current}, title \"{Banner.ShownTitle}\", dimmed early {_dimmed})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        Input.ActionRelease(InputActions.Interact);
        foreach (string problem in _problems)
        {
            GD.PushError($"PLATE CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"PLATE OK: {_steps.Count} steps (offered on screen, read into the banner, glowing while shown, settled after)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
