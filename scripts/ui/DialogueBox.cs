using Godot;

namespace TheDeep;

/// <summary>
/// The dialogue box: small, low in the screen, on the style's plate: who speaks (small, in the room's accent), their line revealed
/// letter by letter, and a quiet mark once it is whole (a press moves on). Eased in and out. It only shows what it is given
/// (<see cref="Conversations"/> plays the conversation); text comes from the line book, written in the shared <see cref="TextStyle"/>.
/// </summary>
public partial class DialogueBox : CanvasLayer
{
    /// <summary>The world played: its room's biome gives the accent.</summary>
    [Export] public World World { get; set; } = null!;

    private Control? _root;
    private Control Root => _root ??= new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
    private PanelContainer? _panel;
    private PanelContainer Panel => _panel ??= new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
    private Label? _speaker;
    private Label SpeakerLabel => _speaker ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Uppercase = true };
    private Label? _text;
    private Label TextLabel => _text ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private Control? _more;

    /// <summary>The mark of a whole line: a small triangle pointing down, drawn (the font has no such glyph).</summary>
    private Control More => _more ??= new Control
    {
        MouseFilter = Control.MouseFilterEnum.Ignore,
        CustomMinimumSize = new Vector2(0f, TextStyle.Shared.Caption * 0.8f),
    };

    private StyleBoxFlat? _plate;
    private Color _accent;
    private float _shown;
    private bool _open;
    private float _time;

    /// <summary>A conversation's text is on screen (or fading in).</summary>
    public bool IsOpen => _open;

    public override void _Ready()
    {
        TextStyle style = TextStyle.Shared;
        Layer = 76;
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(Root);
        _plate = style.NewPlate();
        Panel.AddThemeStyleboxOverride("panel", _plate);
        Panel.AnchorLeft = 0.5f;
        Panel.AnchorRight = 0.5f;
        Panel.AnchorTop = 1f;
        Panel.AnchorBottom = 1f;
        Panel.OffsetLeft = -340f;
        Panel.OffsetRight = 340f;
        Panel.OffsetTop = -190f;
        Panel.OffsetBottom = -96f;
        Panel.GrowVertical = Control.GrowDirection.Begin;
        var lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", 4);
        style.Dress(SpeakerLabel, style.Caption, style.Ink); // its ink: the accent, once a room's is known (Show)
        style.Dress(TextLabel, style.Body, style.Ink);
        TextLabel.CustomMinimumSize = new Vector2(620f, 0f);
        More.Draw += DrawMore;
        lines.AddChild(SpeakerLabel);
        lines.AddChild(TextLabel);
        lines.AddChild(More);
        Panel.AddChild(lines);
        Root.AddChild(Panel);
        Root.Modulate = Colors.White with { A = 0f };
        Visible = false;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            _plate?.Dispose();
        }
    }

    private void DrawMore()
    {
        float h = More.Size.Y * 0.5f;
        Vector2 tip = new(More.Size.X - h, More.Size.Y * 0.75f);
        More.DrawColoredPolygon(new[] { tip, tip + new Vector2(-h * 0.6f, -h), tip + new Vector2(h * 0.6f, -h) }, _accent);
    }

    /// <summary>Shows <paramref name="speaker"/>'s <paramref name="text"/>, its first <paramref name="shown"/> letters.</summary>
    public void Show(string speaker, string text, int shown, bool whole)
    {
        _open = true;
        Visible = true;
        Color accent = TextStyle.Shared.Accent(World.Room.Biome);
        if (accent != _accent)
        {
            _accent = accent;
            SpeakerLabel.AddThemeColorOverride("font_color", accent);
            More.QueueRedraw();
        }

        SpeakerLabel.Text = speaker;
        TextLabel.Text = text;
        TextLabel.VisibleCharacters = whole ? -1 : shown;
        More.Visible = whole;
    }

    /// <summary>Closes the box: it fades out.</summary>
    public void Close() => _open = false;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _shown = Motion.Approach(_shown, _open ? 1f : 0f, dt / TextStyle.Shared.Panel);
        Root.Modulate = Colors.White with { A = _shown * _shown * (3f - 2f * _shown) };
        More.Modulate = Colors.White with { A = 0.55f + 0.45f * Mathf.Sin(_time * 4f) };
        Visible = _shown > 0f;
    }
}
