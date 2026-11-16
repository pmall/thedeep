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
public partial class DialogueCheck : StepDriver
{
    /// <summary>The running game's conversations.</summary>
    public required Conversations Conversations { get; init; }
    /// <summary>The running game's dialogue box.</summary>
    public required DialogueBox Box { get; init; }
    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }
    /// <summary>The running game's banner.</summary>
    public required Banner Banner { get; init; }

    private readonly List<string> _speakers = new();
    private readonly HashSet<Vector2> _sizes = new();
    private readonly List<string> _covered = new();
    private int _tapAt = -1;
    private bool _reloaded;
    private bool _cutOff;

    /// <summary>Presses interact for one tick (now), as a tap.</summary>
    private void Tap()
    {
        Press(InputActions.Interact);
        _tapAt = Ticks;
    }

    protected override void Build()
    {
        Conversation? first = World.Room.First<Conversation>();
        if (first is null)
        {
            Fail($"the {World.RoomName} has no conversation");
            return;
        }

        Conversation lamplighter = first;
        int lines = lamplighter.Lines.Length;
        Steps.Add(new("standing in the porch, the Lamplighter comes near and offers to talk, its prompt on screen by the pilgrim", 8f,
            () => Player.Respawn(CheckKit.Porch(lamplighter), 1),
            () => Conversations.Offered == lamplighter && lamplighter.Prompt.Offered && CheckKit.InSight(lamplighter.Prompt, Player)));
        Steps.Add(new("leaving the room mid-conversation ends it: the pilgrim free, the box closed, not remembered as heard", 2f,
            () =>
            {
                Tap();
                Conversations.Ended += _ => _cutOff = true;
            },
            () =>
            {
                if (Conversations.Talking && !_reloaded)
                {
                    _reloaded = true;
                    World.Load(GD.Load<PackedScene>(World.RoomPath(World.RoomName)));
                    lamplighter = World.Room.First<Conversation>() ?? lamplighter;
                    return false;
                }

                return _reloaded && !Conversations.Talking && Player.InControl && !Box.IsOpen && _cutOff && !lamplighter.Spent;
            }));
        Steps.Add(new("standing in the porch again, it is offered again", 8f,
            () => Player.Respawn(CheckKit.Porch(lamplighter), 1),
            () => Conversations.Offered == lamplighter));
        Steps.Add(new("a banner said, interact begins it: the banner gone, the pilgrim held still, the box open on the first line", 1f,
            () =>
            {
                Banner.Show(new Announcement(lamplighter.Lines[0].Speaker, lamplighter.Lines[0].Line));
                Tap();
            },
            () => Conversations.Talking && Box.IsOpen && !Player.InControl && !Banner.Showing && Conversations.Flow is { Index: 0, Shown: > 0 }));
        Steps.Add(new("a press completes the line being revealed, and the Interact glyph shows", 1f,
            Tap,
            () => Conversations.Flow is { Index: 0, Whole: true } && Ticks > 2 && Box.Marked));
        Steps.Add(new($"presses move on through its {lines} lines, then end it", 4f + lines,
            () => { },
            () =>
            {
                if (Conversations.Flow is { } flow && (Ticks - _tapAt) > 6)
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
        Steps.Add(new("the pilgrim is free, the box closed, the conversation remembered", 1f,
            () => { },
            () => Player.InControl && !Box.IsOpen && lamplighter.Spent && Session.Done(World.RoomName, lamplighter.NameText())
                  && _speakers.Count == lines && _speakers.All(name => name.Length > 0) && _sizes.Count == 1 && _covered.Count == 0));
        Steps.Add(new("and it is not offered again", 1f,
            () => { },
            () => Ticks > 45 && Conversations.Offered is null));
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

    protected override string Tag => "DIALOGUE";

    protected override void Entering() => _tapAt = -100;

    /// <summary>Lets go of interact the tick after a tap.</summary>
    protected override void Observe(float delta)
    {
        if (Ticks == _tapAt + 1)
        {
            Release(InputActions.Interact);
        }
    }

    protected override string Detail =>
        $"at {Player.GlobalPosition}, talking {Conversations.Talking}, offered {Conversations.Offered?.NameText()}, line {Conversations.Flow?.Index}, " +
        $"speakers {string.Join(",", _speakers)}, box sizes {string.Join(" ", _sizes)}; {string.Join("; ", _covered)}";

    protected override string Outcome =>
        $"{StepCount} steps (offered, begun and held still, a line completed, the lines in turn with their " +
        "speakers, one box size, clear of both, freed and remembered, not offered again)";
}
