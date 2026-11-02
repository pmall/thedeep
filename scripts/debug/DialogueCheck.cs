using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The dialogue box in place (`-- --room Narthex --dialogue-check`, run by tools/check.sh), through the real input actions: near the
/// Lamplighter, "Talk" is offered; interact begins the conversation (the pilgrim held still, the box open, the first speaker's line
/// revealing); a press completes a line, the next moves on, the speakers in turn; after the last the pilgrim is free, the box closes,
/// and the conversation (heard once) is remembered and no longer offered. Each step has a time limit. Prints `DIALOGUE OK` or
/// `DIALOGUE CHECK` errors.
/// </summary>
public partial class DialogueCheck : Node
{
    public required Player Player { get; init; }
    public required World World { get; init; }
    public required Conversations Conversations { get; init; }
    public required DialogueBox Box { get; init; }
    public required Session Session { get; init; }

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private readonly List<string> _speakers = new();
    private int _index = -1;
    private int _ticks;
    private int _tapAt = -1;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    public DialogueCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    /// <summary>Presses interact for one tick (now), as a tap.</summary>
    private void Tap()
    {
        Input.ActionPress(InputActions.Interact);
        _tapAt = _ticks;
    }

    public override void _Ready()
    {
        Conversation? lamplighter = World.Room.All<Conversation>().FirstOrDefault();
        if (lamplighter is null)
        {
            _problems.Add("the Narthex has no conversation");
            Finish();
            return;
        }

        int lines = lamplighter.Lines.Length;
        _steps.Add(new("standing in the porch, the Lamplighter comes near and offers to talk", 8f,
            () => Player.Respawn(new Vector3(19f, 0f, 0f), 1),
            () => Conversations.Offered == lamplighter));
        _steps.Add(new("interact begins it: the pilgrim held still, the box open on the first line", 1f,
            Tap,
            () => Conversations.Talking && Box.IsOpen && !Player.InControl && Conversations.Flow is { Index: 0, Shown: > 0 }));
        _steps.Add(new("a press completes the line being revealed", 1f,
            Tap,
            () => Conversations.Flow is { Index: 0, Whole: true }));
        _steps.Add(new($"presses move on through its {lines} lines, then end it", 4f + lines,
            () => { },
            () =>
            {
                if (Conversations.Flow is { } flow && (_ticks - _tapAt) > 6)
                {
                    TextLine? line = LineBook.Shared.Find(lamplighter.Lines[flow.Index]);
                    if (_speakers.Count <= flow.Index)
                    {
                        _speakers.Add(line?.Speaker ?? "");
                    }

                    Tap();
                }

                return !Conversations.Talking;
            }));
        _steps.Add(new("the pilgrim is free, the box closed, the conversation remembered", 1f,
            () => { },
            () => Player.InControl && !Box.IsOpen && lamplighter.Spent && Session.Save.Collected.Contains($"Narthex/{lamplighter.Name}")
                  && _speakers.Count == lines && _speakers.Distinct().Count() > 1));
        _steps.Add(new("and it is not offered again", 1f,
            () => { },
            () => _ticks > 45 && Conversations.Offered is null));
        Next();
    }

    private void Next()
    {
        _index++;
        _ticks = 0;
        _tapAt = -100;
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

        if (_ticks == _tapAt + 1)
        {
            Input.ActionRelease(InputActions.Interact);
        }

        Step step = _steps[_index];
        _ticks++;
        if (step.Done())
        {
            Next();
        }
        else if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, talking " +
                          $"{Conversations.Talking}, offered {Conversations.Offered?.Name}, line {Conversations.Flow?.Index}, speakers " +
                          $"{string.Join(",", _speakers)})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        Input.ActionRelease(InputActions.Interact);
        foreach (string problem in _problems)
        {
            GD.PushError($"DIALOGUE CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"DIALOGUE OK: {_steps.Count} steps (offered, begun and held still, a line completed, the lines in turn with their " +
                     "speakers, freed and remembered, not offered again)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
