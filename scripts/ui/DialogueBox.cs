using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The dialogue box, on the style's plate: who speaks (a caption in the room's accent, <see cref="TextStyle.DressSpeaker"/>), their
/// line revealed letter by letter, and once it is whole the Interact button's glyph at the plate's corner (a press moves on), drawn as
/// the prompts draw it (<see cref="ButtonGlyph"/>, following the device last used). It stands in the banner's places
/// (<see cref="TextStyle.Places"/>): high by default, low while the high place would cover the pilgrim or the one speaking
/// (<see cref="DialoguePlace"/>). It keeps one size for a whole conversation, the size of its widest name and its longest line
/// (<see cref="Open"/>), up to <see cref="MaxWidth"/>, a line that wraps taken as wide as its rows come out even (<see cref="EvenRows"/>). Eased in and out. It only shows what it is given (<see cref="Conversations"/>
/// plays the conversation); text comes from the line book, written in the shared <see cref="TextStyle"/>.
/// </summary>
public partial class DialogueBox : CanvasLayer
{
    /// <summary>The world played: its room's biome gives the accent.</summary>
    [Export] public World World { get; set; } = null!;

    [Export] public Player Player { get; set; } = null!;
    [Export] public Camera3D Camera { get; set; } = null!;

    /// <summary>The widest the plate stands, its padding included, pixels on the game's 1600-wide canvas.</summary>
    [Export] public float MaxWidth { get; set; } = 760f;

    /// <summary>How far the one speaking reaches around its conversation's origin, meters (it stays clear of the box).</summary>
    [Export] public Vector2 SpeakerSize { get; set; } = new(1.6f, 1.6f);

    /// <summary>The glyph's size against the controls manual's.</summary>
    [Export] public float GlyphScale { get; set; } = 1.8f;

    private Control? _root;
    private Control Root => _root ??= new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
    private PanelContainer? _panel;
    private PanelContainer Panel => _panel ??= new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
    private Label? _speaker;
    private Label SpeakerLabel => _speaker ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
    private Label? _text;
    private Label TextLabel => _text ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = Wrap };
    private Control? _more;
    private Control More => _more ??= new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
    private ButtonPainter? _painter;
    private ButtonPainter Painter => _painter ??= new ButtonPainter(GlyphScale);

    private const TextServer.AutowrapMode Wrap = TextServer.AutowrapMode.WordSmart;

    private const TextServer.LineBreakFlag Breaks = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Adaptive;

    private static TextStyle Style => TextStyle.Shared;

    private StyleBoxFlat? _plate;
    private Color _accent;
    private float _shown;
    private bool _open;
    private bool _low;
    private bool _placed;
    private float _time;
    private Node3D? _speakerNode;
    private ButtonNames.Device _device;
    private ButtonGlyph? _glyph;
    private ButtonNames.Device _glyphDevice;

    /// <summary>A conversation's text is on screen (or fading in).</summary>
    public bool IsOpen => _open;

    /// <summary>Where the plate stands on the canvas now, pixels.</summary>
    public Rect2 Plate => Panel.GetGlobalRect();

    /// <summary>The once-whole mark shows (the line is whole).</summary>
    public bool Marked => More.Visible;

    public override void _Ready()
    {
        Layer = 76;
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(Root);
        _plate = Style.NewPlate();
        Panel.AddThemeStyleboxOverride("panel", _plate);
        var lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", 4);
        Style.DressSpeaker(SpeakerLabel, Style.Ink); // its ink: the accent, once a room's is known (Show)
        Style.Dress(TextLabel, Style.Body, Style.Ink);
        lines.AddChild(SpeakerLabel);
        lines.AddChild(TextLabel);
        Panel.AddChild(lines);
        Root.AddChild(Panel);
        More.Draw += DrawMore;
        Root.AddChild(More);
        Root.Modulate = Colors.White with { A = 0f };
        _device = ButtonGlyph.FirstDevice();
        Visible = false;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            _plate?.Dispose();
            _painter?.Dispose();
        }
    }

    public override void _Input(InputEvent @event) => _device = ButtonGlyph.DeviceAfter(_device, @event);

    /// <summary>
    /// Opens on a conversation spoken by <paramref name="speaker"/> (null: no one on screen to keep clear of), sized once for all its
    /// lines: its names <paramref name="speakers"/> and its words <paramref name="texts"/>, as the book gives them.
    /// </summary>
    public void Open(Node3D? speaker, IReadOnlyList<string> speakers, IReadOnlyList<string> texts)
    {
        _speakerNode = speaker;
        Font font = Style.Font;
        float widest = 0f;
        foreach (string name in speakers)
        {
            widest = Mathf.Max(widest, font.GetStringSize(name.ToUpperInvariant(), HorizontalAlignment.Left, -1f, Style.Caption).X);
        }

        foreach (string text in texts)
        {
            // A line that wraps, as wide as its rows come out even (EvenRows): no word left alone on the last.
            float natural = font.GetStringSize(text, HorizontalAlignment.Left, -1f, Style.Body).X;
            widest = Mathf.Max(widest, EvenRows.Width(w => RowsOf(text, w), natural, TextWidth));
        }

        float width = Mathf.Min(Mathf.Ceil(widest) + 2f, TextWidth);
        int rows = 1;
        foreach (string text in texts)
        {
            rows = Mathf.Max(rows, RowsOf(text, width));
        }

        // As tall as the label stands with its longest line: its rows and the spacing between them.
        float height = rows * font.GetHeight(Style.Body) + (rows - 1) * TextLabel.GetThemeConstant("line_spacing");
        SpeakerLabel.CustomMinimumSize = new Vector2(width, 0f);
        TextLabel.CustomMinimumSize = new Vector2(width, Mathf.Ceil(height));
        Panel.ResetSize();
        _placed = false;
    }

    /// <summary>The widest the text stands within the plate, pixels.</summary>
    private float TextWidth => MaxWidth - 2f * Style.Padding.X;

    /// <summary>How many rows <paramref name="text"/> takes as the label wraps it at <paramref name="width"/>.</summary>
    private static int RowsOf(string text, float width)
    {
        Font font = Style.Font;
        float tall = font.GetMultilineStringSize(text, HorizontalAlignment.Left, width, Style.Body, -1, Breaks).Y;
        return Mathf.RoundToInt(tall / font.GetHeight(Style.Body));
    }

    /// <summary>Shows <paramref name="speaker"/>'s <paramref name="text"/>, its first <paramref name="shown"/> letters.</summary>
    public void Show(string speaker, string text, int shown, bool whole)
    {
        _open = true;
        Visible = true;
        Color accent = Style.Accent(World.Room.Biome);
        if (accent != _accent)
        {
            _accent = accent;
            Style.DressSpeaker(SpeakerLabel, accent);
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
        _shown = Motion.Approach(_shown, _open ? 1f : 0f, dt / Style.Panel);
        Root.Modulate = Colors.White with { A = _shown * _shown * (3f - 2f * _shown) };
        Visible = _shown > 0f;
        if (!Visible)
        {
            return;
        }

        Place(dt);
        if (_glyph is null || _device != _glyphDevice)
        {
            _glyph = ButtonGlyph.Main(InputActions.Interact, _device);
            _glyphDevice = _device;
        }

        if (_glyph is { } shownGlyph)
        {
            shownGlyph.Light(dt, RestLit);
            float size = Painter.CapHeight + 4f * GlyphScale;
            More.Size = new Vector2(Painter.Width(Style.Font, shownGlyph) + 4f * GlyphScale, size);
            // On the plate's lower right corner, half over its edge, so it reads as the box's own.
            More.Position = Panel.Position + Panel.Size - new Vector2(More.Size.X + Style.Padding.X * 0.5f, More.Size.Y * 0.5f);
            More.QueueRedraw();
        }
    }

    /// <summary>How lit the glyph stands unheld: a pale cap with dark letters, as the prompts show it.</summary>
    private const float RestLit = 0.8f;

    private void DrawMore()
    {
        if (_glyph is { } glyph)
        {
            // A slow breath, never fading out of sight.
            float a = 0.85f + 0.15f * Mathf.Sin(_time * 3f);
            Painter.Draw(More, Style.Font, glyph, More.Size * 0.5f, a, Vector2.Zero);
        }
    }

    /// <summary>High, or low while the high place would cover the pilgrim or the speaker; set at once as it opens, eased after.</summary>
    private void Place(float dt)
    {
        Vector2 canvas = GetViewport().GetVisibleRect().Size;
        Vector2 size = Panel.Size;
        float x = Mathf.Round((canvas.X - size.X) * 0.5f);
        TextPlaces places = Style.Places;
        float lowTop = places.BannerLowBottom(canvas.Y, Style.RowPlate(Style.Body)) - size.Y;
        Box high = new(x, places.BannerTop, x + size.X, places.BannerTop + size.Y);
        Box low = new(x, lowTop, x + size.X, lowTop + size.Y);
        Box pilgrim = OnScreen(Player.Body);
        Box speaker = _speakerNode is { } node && IsInstanceValid(node)
            ? OnScreen(new Box(node.GlobalPosition.X - SpeakerSize.X * 0.5f, node.GlobalPosition.Y - SpeakerSize.Y * 0.5f,
                node.GlobalPosition.X + SpeakerSize.X * 0.5f, node.GlobalPosition.Y + SpeakerSize.Y * 0.5f))
            : new Box(-1f, -1f, -1f, -1f);
        // Decided as it opens and kept while it shows, so the box never jumps while it is read.
        if (!_placed)
        {
            _low = DialoguePlace.Low(high, low, pilgrim, speaker);
        }

        float y = _low ? lowTop : places.BannerTop;
        float ease = _placed ? 1f - Mathf.Exp(-dt / Style.Panel) : 1f;
        Panel.Position = new Vector2(x, Mathf.Lerp(Panel.Position.Y, y, ease));
        _placed = true;
    }

    /// <summary>A box on the play plane as it stands on the canvas, pixels, y down.</summary>
    private Box OnScreen(Box world)
    {
        Vector2 a = Camera.UnprojectPosition(new Vector3(world.MinX, world.MaxY, 0f));
        Vector2 b = Camera.UnprojectPosition(new Vector3(world.MaxX, world.MinY, 0f));
        return new Box(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y), Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y));
    }
}
