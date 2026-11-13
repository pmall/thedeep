using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Conversations in play: standing near a <see cref="Conversation"/> (and no shrine or plate prompting) shows "Talk"; talking
/// (interact) holds the pilgrim still and plays its lines in the <see cref="DialogueBox"/> (the core's <see cref="DialogueFlow"/>):
/// interact or jump completes the line being revealed, then moves on; after the last line the pilgrim is free again, and a
/// conversation heard <see cref="Conversation.Once"/> is remembered (the session keeps it). Talking dismisses the banner, so the two
/// never stand on screen together. Each line is revealed at the pace these exports set (<see cref="RevealPace"/>).
/// </summary>
public partial class Conversations : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public Session Session { get; set; } = null!;
    [Export] public DialogueBox Box { get; set; } = null!;
    [Export] public Banner Banner { get; set; } = null!;

    /// <summary>How fast a line reveals, letters per second, and how long it rests after a sentence's end and a comma, seconds.</summary>
    [ExportGroup("Reveal")]
    [Export] public float LettersPerSecond { get; set; } = 45f;

    [Export] public float SentencePause { get; set; } = 0.32f;

    [Export] public float CommaPause { get; set; } = 0.14f;

    private Conversation? _prompted;
    private Conversation? _talking;
    private DialogueFlow? _flow;
    private IReadOnlyList<string> _speakers = System.Array.Empty<string>();

    /// <summary>A conversation is being played (the pilgrim held still).</summary>
    public bool Talking => _talking is not null;

    /// <summary>The conversation offered now ("Talk" shown), if any.</summary>
    public Conversation? Offered => _prompted;

    /// <summary>The conversation being played, as the core sees it.</summary>
    public DialogueFlow? Flow => _flow;

    /// <summary>Raised when a conversation begins, and when it has been heard to its end.</summary>
    public event System.Action<Conversation>? Began;
    /// <summary>Raised when a conversation has been heard to its end.</summary>
    public event System.Action<Conversation>? Ended;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree() => World.RoomLoaded -= OnRoomLoaded;

    public override void _PhysicsProcess(double delta)
    {
        if (_talking is { } talking && _flow is { } flow)
        {
            Play(talking, flow, (float)delta);
            return;
        }

        Conversation? near = Player.InControl && Player.IsUpright && Player.IsOnFloor() && !Session.Prompting ? Nearest() : null;
        Prompt(near);
        near?.Place(Player.GlobalPosition);
        if (near is not null && Input.IsActionJustPressed(InputActions.Interact))
        {
            Begin(near);
        }
    }

    private Conversation? Nearest()
    {
        IReadOnlyList<Conversation> found = World.Room.All<Conversation>();
        Vector3 p = Player.GlobalPosition;
        for (int i = 0; i < found.Count; i++)
        {
            if (!found[i].Spent && found[i].Near(p))
            {
                return found[i];
            }
        }

        return null;
    }

    private void Begin(Conversation conversation)
    {
        Prompt(null);
        LineBook book = LineBook.Shared;
        _speakers = conversation.Lines.Select(l => book.Find(l.Speaker)?.Text ?? "").ToList();
        List<string> texts = conversation.Lines.Select(l => book.Find(l.Line)?.Text ?? "").ToList();
        _flow = new DialogueFlow(texts, new RevealPace(LettersPerSecond, SentencePause, CommaPause));
        _talking = conversation;
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        Banner.Clear();
        Box.Open(conversation, _speakers, texts);
        Show(_flow);
        Began?.Invoke(conversation);
    }

    private void Play(Conversation conversation, DialogueFlow flow, float dt)
    {
        flow.Tick(dt);
        if (Input.IsActionJustPressed(InputActions.Interact) || Input.IsActionJustPressed(InputActions.Jump))
        {
            if (flow.Press() == DialoguePress.Ended)
            {
                End(conversation);
                return;
            }
        }

        Show(flow);
    }

    private void Show(DialogueFlow flow) => Box.Show(_speakers[flow.Index], flow.Text, flow.Shown, flow.Whole);

    private void End(Conversation conversation)
    {
        Box.Close();
        _talking = null;
        _flow = null;
        Player.Frozen = false;
        if (conversation.Once)
        {
            conversation.Spent = true;
            Session.Remember(conversation.Name);
        }

        Ended?.Invoke(conversation);
    }

    private void Prompt(Conversation? conversation)
    {
        if (_prompted == conversation)
        {
            return;
        }

        if (_prompted is { } shown && IsInstanceValid(shown))
        {
            shown.Prompt.Offer(false);
        }

        _prompted = conversation;
        conversation?.Prompt.Offer(true);
    }

    private void OnRoomLoaded(Room room)
    {
        _prompted = null;
        if (_talking is not null)
        {
            Box.Close();
            _talking = null;
            _flow = null;
        }

        string name = World.NameOf(room.SceneFilePath);
        foreach (Conversation conversation in room.All<Conversation>())
        {
            conversation.Spent = conversation.Once && Session.Done(name, conversation.Name);
        }
    }
}
