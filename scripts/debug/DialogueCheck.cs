using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The dialogue box in place (`-- --room Narthex --dialogue-check`, run by tools/check.sh; the sequel world's Quay too), through the
/// real input actions: near the room's first conversation (a Lamplighter, flying), "Talk" is offered on screen, by the pilgrim; a
/// banner showing, interact begins the conversation (the banner dismissed, the pilgrim held still, the box open on the first line,
/// clear of the pilgrim and of the speaker); a press completes a line (the Interact glyph then shows), the next moves on, each line
/// named by its speaker's line, the box one size throughout; after the last the pilgrim is free, the box closes, and the conversation
/// (heard once) is remembered and no longer offered. Each step has a time limit. Prints `DIALOGUE OK` or `DIALOGUE CHECK` errors.
/// </summary>
public partial class DialogueCheck : Node
{
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }
    /// <summary>The running game's conversations.</summary>
    public required Conversations Conversations { get; init; }
    /// <summary>The running game's dialogue box.</summary>
    public required DialogueBox Box { get; init; }
    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }
    /// <summary>The running game's banner.</summary>
    public required Banner Banner { get; init; }

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private readonly List<string> _speakers = new();
    private readonly HashSet<Vector2> _sizes = new();
    private readonly List<string> _covered = new();
    private int _index = -1;
    private int _ticks;
    private int _tapAt = -1;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    /// <summary>Runs before the pilgrim each frame and each physics tick, so what it presses is read the same tick.</summary>
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
        Conversation? lamplighter = World.Room.First<Conversation>();
        if (lamplighter is null)
        {
            _problems.Add($"the {World.RoomName} has no conversation");
            Finish();
            return;
        }

        int lines = lamplighter.Lines.Length;
        _steps.Add(new("standing in the porch, the Lamplighter comes near and offers to talk, its prompt on screen by the pilgrim", 8f,
            () => Player.Respawn(Porch(lamplighter), 1),
            () => Conversations.Offered == lamplighter && lamplighter.Prompt.Offered && PromptInSight(lamplighter.Prompt)));
        _steps.Add(new("a banner said, interact begins it: the banner gone, the pilgrim held still, the box open on the first line", 1f,
            () =>
            {
                Banner.Show(new Announcement(lamplighter.Lines[0].Speaker, lamplighter.Lines[0].Line));
                Tap();
            },
            () => Conversations.Talking && Box.IsOpen && !Player.InControl && !Banner.Showing && Conversations.Flow is { Index: 0, Shown: > 0 }));
        _steps.Add(new("a press completes the line being revealed, and the Interact glyph shows", 1f,
            Tap,
            () => Conversations.Flow is { Index: 0, Whole: true } && _ticks > 2 && Box.Marked));
        _steps.Add(new($"presses move on through its {lines} lines, then end it", 4f + lines,
            () => { },
            () =>
            {
                if (Conversations.Flow is { } flow && (_ticks - _tapAt) > 6)
                {
                    if (_speakers.Count <= flow.Index)
                    {
                        _speakers.Add(LineBook.Shared.Find(lamplighter.Lines[flow.Index].Speaker)?.Text ?? "");
                    }

                    _sizes.Add(Box.Plate.Size);
                    Covering(lamplighter);
                    Tap();
                }

                return !Conversations.Talking;
            }));
        _steps.Add(new("the pilgrim is free, the box closed, the conversation remembered", 1f,
            () => { },
            () => Player.InControl && !Box.IsOpen && lamplighter.Spent && Session.Save.Collected.Contains($"{World.RoomName}/{lamplighter.Name}")
                  && _speakers.Count == lines && _speakers.All(name => name.Length > 0) && _sizes.Count == 1 && _covered.Count == 0));
        _steps.Add(new("and it is not offered again", 1f,
            () => { },
            () => _ticks > 45 && Conversations.Offered is null));
        Next();
    }

    /// <summary>Where the pilgrim stands to be talked to: on the floor under the conversation, a little short of it.</summary>
    public static Vector3 Porch(Conversation conversation)
    {
        Vector3 from = conversation.GlobalPosition + new Vector3(-2f, 0.5f, 0f);
        var ray = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * 30f, PhysicsLayers.Solid);
        Godot.Collections.Dictionary hit = conversation.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count > 0 ? hit["position"].AsVector3() with { Z = 0f } : new Vector3(from.X, 0f, 0f);
    }

    private bool PromptInSight(Node3D prompt) => InSight(prompt, Player);

    /// <summary>Whether <paramref name="prompt"/> stands on screen, within a few meters of <paramref name="player"/>.</summary>
    public static bool InSight(Node3D prompt, Player player)
    {
        Camera3D camera = prompt.GetViewport().GetCamera3D();
        Rect2 screen = prompt.GetViewport().GetVisibleRect();
        Vector3 at = prompt.GlobalPosition;
        return !camera.IsPositionBehind(at) && screen.HasPoint(camera.UnprojectPosition(at))
               && new Vector2(at.X - player.GlobalPosition.X, at.Y - player.GlobalPosition.Y).Length() < 3.5f;
    }

    /// <summary>Notes it if the box covers the pilgrim's body or the speaker's origin on screen.</summary>
    private void Covering(Conversation conversation)
    {
        Camera3D camera = GetViewport().GetCamera3D();
        Rect2 plate = Box.Plate;
        Box body = Player.Body;
        foreach (Vector3 point in new[] { new Vector3(body.MinX, body.MinY, 0f), new Vector3(body.MaxX, body.MaxY, 0f), conversation.GlobalPosition })
        {
            if (plate.HasPoint(camera.UnprojectPosition(point)))
            {
                _covered.Add($"the box at {plate} covers {point}");
            }
        }
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
                          $"{string.Join(",", _speakers)}, box sizes {string.Join(" ", _sizes)}; {string.Join("; ", _covered)})");
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
                     "speakers, one box size, clear of both, freed and remembered, not offered again)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
