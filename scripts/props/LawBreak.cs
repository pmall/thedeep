using System;
using Godot;

namespace TheDeep;

/// <summary>
/// One of the three laws holding the pilgrim back (docs/theme.md#law-per-act): at the lip of a lethal drop (the third law), the
/// pilgrim's own refusal stands as a veil of glyphs it cannot walk through, and the law's words rise as it nears, in the style's
/// <see cref="TextStyle.WorldFont"/>, drawn over whatever stands before them, so no solid of the room ever hides them. Breaking it is
/// an act, never a push (docs/gameplay.md#law-act): pressing on only strains the veil (up to <see cref="PushStrain"/>, never
/// breaking it), and the first time the pilgrim says why it may not (<see cref="RefusalLine"/>, raised as <see cref="Refused"/>
/// for the hint line). From then on, standing at the lip offers the act (<see cref="Prompt"/>, a hold naming it,
/// <see cref="ActLine"/>); the veil strains with the hold, and the hold full breaks the law: the veil scatters, the words go
/// out, and the way is open for good (<see cref="Session"/> remembers it by this node's room and name, and calls
/// <see cref="Settle"/> on later visits). A story beat, not an ability gate (docs/theme.md#laws-outside-abilities): the veil is
/// not a <see cref="Block"/>, and the map counts the way as open; the world's network names it as a <c>milestone</c>, which the
/// draft check holds to story order.
/// </summary>
public partial class LawBreak : Node3D, IAnnounces, IHints
{
    [Export] public StaticBody3D Fence { get; set; } = null!;
    [Export] public Label3D Words { get; set; } = null!;
    [Export] public Node3D Veil { get; set; } = null!;
    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>The act offered at the lip, a hold (its length is the brick's tuning, on the scene).</summary>
    [Export] public ActionPrompt Prompt { get; set; } = null!;

    /// <summary>The law's words (shown at the lip), and the banner when it breaks.</summary>
    [Export] public StringName LawLine { get; set; } = "";
    [Export] public StringName BrokenLine { get; set; } = "";
    [Export] public StringName BrokenHintLine { get; set; } = "";

    /// <summary>What the pilgrim says, once, the first time it presses on: why it may not.</summary>
    [Export] public StringName RefusalLine { get; set; } = "";

    /// <summary>The act the prompt names ("Break the third law").</summary>
    [Export] public StringName ActLine { get; set; } = "";

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds => new[] { LawLine, BrokenLine, BrokenHintLine, RefusalLine, ActLine };

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> HintLines => new[] { RefusalLine };

    /// <summary>What the banner says once it is broken.</summary>
    public Announcement Said => new(BrokenLine, BrokenHintLine);

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<Announcement> Announcements => new[] { Said };

    /// <summary>The color of the law's words shown, and of the mark beside the pilgrim's refusal.</summary>
    public static readonly Color WordsColor = new(0.86f, 0.84f, 1f);

    /// <summary>Which way the forbidden side lies from the node (+1 east, −1 west).</summary>
    [Export] public int Toward { get; set; } = 1;

    /// <summary>The words start to show this far from the lip, meters.</summary>
    [Export] public float Near { get; set; } = 6f;

    /// <summary>How far pressing on strains the law, 0–1: enough to see it hold, never to break it.</summary>
    [Export] public float PushStrain { get; set; } = 0.5f;

    /// <summary>Seconds of pressing on for the strain to reach <see cref="PushStrain"/>, and the pilgrim to say why it may not.</summary>
    [Export] public float PushTime { get; set; } = 0.5f;

    /// <summary>How near the lip the pilgrim stands to press on it, or be offered the act, meters.</summary>
    [Export] public float Lip { get; set; } = 1.2f;

    /// <summary>The words' color before they show: the dark of the room around the lip.</summary>
    private static readonly Color Dark = new(0.12f, 0.11f, 0.16f);

    private float _push;
    private float _strain;
    private float _shown;
    private float _time;
    private float _gone = -1f;
    private float _baseEnergy;
    private Vector3 _wordsAt;

    /// <summary>The pilgrim, set by the session when the room loads.</summary>
    public Player? Player { get; set; }

    /// <summary>The law is broken: the veil is gone and the way open.</summary>
    public bool Broken => _gone >= 0f;

    /// <summary>0 to 1 while the pilgrim presses against the law (never above <see cref="PushStrain"/>) or holds the act.</summary>
    public float Strain => _strain;

    /// <summary>The pilgrim has said why it may not, this visit: the act is offered at the lip.</summary>
    public bool Refused { get; private set; }

    /// <summary>Raised once a visit, the first time the pilgrim presses on: it says <see cref="RefusalLine"/>.</summary>
    public event Action<LawBreak>? Refusal;

    /// <summary>Raised once, as the law breaks.</summary>
    public event Action<LawBreak>? Broke;

    public override void _Ready()
    {
        Fence.CollisionLayer = PhysicsLayers.WorldRock;
        Fence.CollisionMask = 0;
        Fence.Position = new Vector3(Toward * 0.25f, 0f, 0f);
        Words.Font = TextStyle.Shared.WorldFont;
        Words.Text = LineBook.Shared.Find(LawLine)?.Text ?? "";
        Words.Position = Words.Position with { X = -Toward * Mathf.Abs(Words.Position.X) };
        _wordsAt = Words.Position;
        _baseEnergy = Light.LightEnergy;
        Prompt.Position = Prompt.Position with { X = -Toward * Mathf.Abs(Prompt.Position.X) };
        Prompt.Reach = -Toward;
        Prompt.Verb = ActLine;
        Prompt.Completed += Break;
        Apply();
    }

    /// <summary>Broken before: the way is open, nothing shows.</summary>
    public void Settle()
    {
        _gone = 10f;
        _strain = 0f;
        Prompt.Offer(false);
        Fence.ProcessMode = ProcessModeEnum.Disabled;
        Apply();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        if (Broken)
        {
            _gone += dt;
            Apply();
            return;
        }

        float away = float.PositiveInfinity;
        bool atLip = false;
        bool pressing = false;
        if (Player is { } player)
        {
            away = (GlobalPosition.X - player.GlobalPosition.X) * Toward;
            atLip = player.InControl && player.IsUpright && player.IsOnFloor() && away > -0.2f && away < Lip
                    && Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) < 1.5f;
            pressing = atLip && player.CurrentInput.MoveX == Toward;
        }

        _shown = Motion.Approach(_shown, away < Near && away > -1f ? 1f : 0f, dt * 1.5f);
        _push = Mathf.Clamp(_push + (pressing ? dt : -dt * 0.5f) * PushStrain / PushTime, 0f, PushStrain);
        if (!Refused && _push >= PushStrain)
        {
            Refused = true;
            Refusal?.Invoke(this);
        }

        Prompt.Offer(Refused && atLip);
        _strain = Mathf.Max(_push, Prompt.Fill);
        Apply();
    }

    /// <summary>The act held full: the fence goes, and the law breaks.</summary>
    private void Break()
    {
        if (Broken)
        {
            return;
        }

        _gone = 0f;
        _strain = 1f;
        Prompt.Offer(false);
        Fence.ProcessMode = ProcessModeEnum.Disabled;
        Broke?.Invoke(this);
    }

    private void Apply()
    {
        float s = _strain * _strain;
        // Breaking: the words flare white and drift up as they go out; the veil scatters upward.
        float gone = Broken ? Mathf.Clamp(_gone / 1.2f, 0f, 1f) : 0f;
        float shown = _shown * _shown * (3f - 2f * _shown) * (1f - gone);
        float tremble = s * 0.05f;
        Words.Position = _wordsAt + new Vector3(Mathf.Sin(_time * 41f) * tremble, Mathf.Sin(_time * 29f) * tremble + gone * 1.5f, 0f);
        // They fade in by color, from the dark of the room, so their outline never shows before them.
        Words.Visible = shown > 0.02f;
        Words.Modulate = Dark.Lerp(WordsColor.Lerp(Colors.White, s), shown);
        Veil.Visible = gone < 1f;
        Veil.Scale = new Vector3(1f + 0.6f * s + 2f * gone, 1f + 0.3f * gone, 1f);
        Veil.Position = new Vector3(0f, 1.5f * gone, 0f);
        Light.LightEnergy = _baseEnergy * (0.3f + 0.7f * shown + 3f * s) * (1f - gone) + (Broken && gone < 1f ? _baseEnergy * 6f * (1f - gone) : 0f);
    }
}
