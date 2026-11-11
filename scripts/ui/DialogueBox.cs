using Godot;

namespace TheDeep;

/// <summary>
/// The dialogue box: small, low in the screen, over a faint veil: who speaks (small, in the accent), their line revealed letter by
/// letter, and a quiet mark once it is whole (a press moves on). Eased in and out. It only shows what it is given
/// (<see cref="Conversations"/> plays the conversation); text comes from the line book.
/// </summary>
public partial class DialogueBox : CanvasLayer
{
    [Export] public Color Accent { get; set; } = new(0.8f, 0.7f, 1f);
    [Export] public Color Words { get; set; } = new(0.93f, 0.91f, 0.97f);
    [Export] public Color Veil { get; set; } = new(0.03f, 0.03f, 0.05f, 0.62f);

    /// <summary>Seconds to ease in or out.</summary>
    [Export] public float FadeTime { get; set; } = 0.2f;

    private Control? _root;
    private Control Root => _root ??= new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
    private PanelContainer? _panel;
    private PanelContainer Panel => _panel ??= new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
    private Label? _speaker;
    private Label SpeakerLabel => _speaker ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Uppercase = true };
    private Label? _text;
    private Label TextLabel => _text ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private Label? _more;
    private Label More => _more ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Text = "▾", HorizontalAlignment = HorizontalAlignment.Right };
    private float _shown;
    private bool _open;
    private float _time;

    /// <summary>A conversation's text is on screen (or fading in).</summary>
    public bool IsOpen => _open;

    public override void _Ready()
    {
        Layer = 76;
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(Root);
        var style = new StyleBoxFlat { BgColor = Veil, ContentMarginLeft = 26f, ContentMarginRight = 26f, ContentMarginTop = 14f, ContentMarginBottom = 14f };
        style.SetCornerRadiusAll(4);
        Panel.AddThemeStyleboxOverride("panel", style);
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
        SpeakerLabel.AddThemeFontSizeOverride("font_size", 15);
        SpeakerLabel.AddThemeColorOverride("font_color", Accent);
        TextLabel.AddThemeFontSizeOverride("font_size", 22);
        TextLabel.AddThemeColorOverride("font_color", Words);
        TextLabel.CustomMinimumSize = new Vector2(620f, 0f);
        More.AddThemeFontSizeOverride("font_size", 14);
        More.AddThemeColorOverride("font_color", Accent);
        lines.AddChild(SpeakerLabel);
        lines.AddChild(TextLabel);
        lines.AddChild(More);
        Panel.AddChild(lines);
        Root.AddChild(Panel);
        Root.Modulate = Colors.White with { A = 0f };
        Visible = false;
    }

    /// <summary>Shows <paramref name="speaker"/>'s <paramref name="text"/>, its first <paramref name="shown"/> letters.</summary>
    public void Show(string speaker, string text, int shown, bool whole)
    {
        _open = true;
        Visible = true;
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
        _shown = Motion.Approach(_shown, _open ? 1f : 0f, dt / FadeTime);
        Root.Modulate = Colors.White with { A = _shown * _shown * (3f - 2f * _shown) };
        More.Modulate = Colors.White with { A = 0.55f + 0.45f * Mathf.Sin(_time * 4f) };
        Visible = _shown > 0f;
    }
}
