using Godot;

namespace TheDeep;

/// <summary>
/// A title and a line on the style's plate, high on the screen, eased in, held, eased out: an ability or an item taken, a boss named
/// or fallen, a scripture read. Words spoken to the pilgrim (<see cref="Voice.Spoken"/>, a <see cref="Proclamation"/>) have no title:
/// the speaker's name stands over them as a caption in the room's accent (<see cref="TextStyle.DressSpeaker"/>), so they never read as
/// a name announced. It stands clear of the play around the pilgrim, and goes low, over the hint line, while the pilgrim is high on
/// the screen, so the pilgrim is never under it (<see cref="BannerPlace"/>; both places: <see cref="TextStyle.Places"/>). A long line
/// wraps within <see cref="MaxWidth"/>, the head on one row and the line in at most <see cref="Rows"/> (the room check holds every
/// line a banner can say to that, <see cref="Fits"/>).
/// Each is held as long as its words take to read (<see cref="ReadingTime"/>); what is said while another shows is read after it,
/// each for its whole time (<see cref="LinesInTurn{T}"/>); a room left drops what shows and what waits (<see cref="Clear"/>).
/// Text always comes from the line book (ids), never from code; it is written in the shared <see cref="TextStyle"/>.
/// </summary>
public partial class Banner : CanvasLayer
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public Camera3D Camera { get; set; } = null!;

    /// <summary>The world played: the room whose accent a speaker's name wears.</summary>
    [Export] public World World { get; set; } = null!;

    /// <summary>The reading pace a banner is held for, letters per second (its title's and its line's), and the least it is held, seconds.</summary>
    [ExportGroup("Hold")]
    [Export] public float LettersPerSecond { get; set; } = 16f;

    [Export] public float MinimumSeconds { get; set; } = 2.5f;

    /// <summary>The widest the plate stands, its padding included, pixels on the game's 1600-wide canvas.</summary>
    [ExportGroup("Box")]
    [Export] public float MaxWidth { get; set; } = 900f;

    /// <summary>The most rows a banner's line wraps to (its title: one).</summary>
    [Export] public int Rows { get; set; } = 3;

    /// <summary>How far below the high place the pilgrim must be before a banner gone low comes back up, pixels.</summary>
    [ExportGroup("Place")]
    [Export] public float Clearance { get; set; } = 48f;

    /// <summary>How many banners may wait behind the one showing.</summary>
    public const int Waits = 2;

    private readonly LinesInTurn<Announcement> _queue = new(Waits);
    private float _shown;
    private float _hold;
    private bool _low;
    private bool _placed;
    private Tween? _tween;

    private Label? _speaker;
    private Label SpeakerLabel => _speaker ??= MakeSpeaker();
    private Label? _title;
    private Label TitleLabel => _title ??= MakeLabel(Style.Title, Style.Ink);
    private Label? _line;
    private Label LineLabel => _line ??= MakeLabel(Style.Body, Style.Dim);
    private PanelContainer? _plate;
    private PanelContainer Plate => _plate ??= new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
    private VBoxContainer? _column;
    private VBoxContainer Column => _column ??= new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };

    private static TextStyle Style => TextStyle.Shared;

    /// <summary>How the line's label wraps, and the same as break flags, so <see cref="Fits"/> measures what the label shows.</summary>
    private const TextServer.AutowrapMode Wrap = TextServer.AutowrapMode.WordSmart;

    private const TextServer.LineBreakFlag Breaks = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Adaptive;

    public override void _Ready()
    {
        Layer = 75;
        Column.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        Column.Position = new Vector2(0f, Style.BannerTop);
        var words = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        words.AddThemeConstantOverride("separation", 2);
        words.AddChild(SpeakerLabel);
        words.AddChild(TitleLabel);
        words.AddChild(LineLabel);
        Plate.AddThemeStyleboxOverride("panel", Style.NewPlate());
        Plate.AddChild(words);
        Column.AddChild(Plate);
        Column.Modulate = Colors.White with { A = 0f };
        AddChild(Column);
    }

    private static Label MakeLabel(int size, Color ink)
    {
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = Wrap, MouseFilter = Control.MouseFilterEnum.Ignore };
        Style.Dress(label, size, ink);
        return label;
    }

    private static Label MakeSpeaker()
    {
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        Style.DressSpeaker(label, Style.Ink); // its ink: the room's accent, once a banner speaks (Present)
        return label;
    }

    /// <summary>Whether a banner is showing (or easing).</summary>
    public bool Showing => _queue.Showing;

    /// <summary>The banner showing, or null.</summary>
    public Announcement? Current => _queue.Showing ? _queue.Current : null;

    /// <summary>How many banners wait behind the one showing.</summary>
    public int Waiting => _queue.Waiting;

    /// <summary>The title's words on screen now (none while words are spoken).</summary>
    public string ShownTitle => TitleLabel.Visible ? TitleLabel.Text : "";

    /// <summary>The speaker's name on screen now, as written in the book (none while a name is announced).</summary>
    public string ShownSpeaker => SpeakerLabel.Visible ? SpeakerLabel.Text : "";

    /// <summary>How opaque the banner is now, 0–1 (eased in: 1).</summary>
    public float Opacity => IsNodeReady() ? Column.Modulate.A : 0f;

    /// <summary>Says <paramref name="said"/>: now, or after the banners already said.</summary>
    public void Show(Announcement said)
    {
        if (_queue.Offer(said))
        {
            Present();
        }
    }

    /// <summary>Gone at once, and nothing waits (a room left).</summary>
    public void Clear()
    {
        _queue.Clear();
        if (!IsNodeReady())
        {
            return;
        }

        _tween?.Kill();
        _tween = null;
        Column.Modulate = Colors.White with { A = 0f };
    }

    /// <summary>
    /// Whether <paramref name="head"/>, said in <paramref name="voice"/>, stands on one row and <paramref name="line"/> wraps to at
    /// most <see cref="Rows"/> rows within the plate, in the shared style's font.
    /// </summary>
    public bool Fits(string head, string line, Voice voice) => RowsOf(HeadText(head, voice), HeadSize(voice)) <= 1 && RowsOf(line, Style.Body) <= Rows;

    /// <summary>The head as shown: a speaker's name in capitals.</summary>
    private static string HeadText(string head, Voice voice) => voice == Voice.Spoken ? head.ToUpperInvariant() : head;

    private static int HeadSize(Voice voice) => voice == Voice.Spoken ? Style.Caption : Style.Title;

    /// <summary>How many rows <paramref name="text"/> takes at <paramref name="size"/> within the plate's widest text.</summary>
    private int RowsOf(string text, int size)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        Vector2 measured = Style.Font.GetMultilineStringSize(text, HorizontalAlignment.Center, TextWidth, size, -1, Breaks);
        return Mathf.RoundToInt(measured.Y / Style.Font.GetHeight(size));
    }

    /// <summary>The widest the text stands within the plate, pixels.</summary>
    private float TextWidth => MaxWidth - 2f * Style.Padding.X;

    /// <summary>The banner showing leaves once read; the plate keeps clear of the pilgrim.</summary>
    public override void _Process(double delta)
    {
        if (!_queue.Showing)
        {
            return;
        }

        Place((float)delta);
        if (_tween is not null)
        {
            return;
        }

        _shown += (float)delta;
        if (_shown >= _hold)
        {
            _tween = CreateTween();
            _tween.TweenProperty(Column, "modulate:a", 0f, _queue.Waiting > 0 ? Style.Hurried : Style.Leave).SetTrans(Tween.TransitionType.Sine);
            _tween.TweenCallback(Callable.From(Left));
        }
    }

    /// <summary>High, or low while the pilgrim is in the high place; set at once as a banner comes in, eased after.</summary>
    private void Place(float dt)
    {
        Box body = Player.Body;
        float pilgrimTop = Camera.UnprojectPosition(new Vector3((body.MinX + body.MaxX) * 0.5f, body.MaxY, 0f)).Y;
        _low = BannerPlace.Low(pilgrimTop, Style.BannerTop + Plate.Size.Y, Clearance, _low && _placed);
        TextPlaces places = Style.Places;
        float y = _low ? places.BannerLowBottom(GetViewport().GetVisibleRect().Size.Y, Style.RowPlate(Style.Body)) - Plate.Size.Y : places.BannerTop;
        float ease = _placed ? 1f - Mathf.Exp(-dt / Style.Panel) : 1f;
        Column.Position = new Vector2(0f, Mathf.Lerp(Column.Position.Y, y, ease));
        _placed = true;
    }

    private void Present()
    {
        Announcement said = _queue.Current;
        LineBook book = LineBook.Shared;
        string head = book.Find(said.Head)?.Text ?? "";
        string line = book.Find(said.Line)?.Text ?? "";
        bool spoken = said.Voice == Voice.Spoken;
        Label headLabel = spoken ? SpeakerLabel : TitleLabel;
        SpeakerLabel.Visible = spoken;
        TitleLabel.Visible = !spoken;
        headLabel.Text = head;
        if (spoken)
        {
            SpeakerLabel.AddThemeColorOverride("font_color", Style.Accent(World.Room.Biome));
        }

        // Spoken words are what is read first; under a title, the line is read after it.
        LineLabel.AddThemeColorOverride("font_color", spoken ? Style.Ink : Style.Dim);
        LineLabel.Text = line;
        LineLabel.Visible = line.Length > 0;

        // The labels as wide as the longer of the two, up to the plate's widest: a short banner keeps a small plate.
        float natural = Mathf.Max(Style.Font.GetStringSize(HeadText(head, said.Voice), HorizontalAlignment.Left, -1, HeadSize(said.Voice)).X,
            Style.Font.GetStringSize(line, HorizontalAlignment.Left, -1, Style.Body).X);
        float width = Mathf.Min(Mathf.Ceil(natural) + 2f, TextWidth);
        headLabel.CustomMinimumSize = new Vector2(width, 0f);
        LineLabel.CustomMinimumSize = new Vector2(width, 0f);
        Plate.ResetSize();

        _hold = ReadingTime.Seconds(head.Length + line.Length, LettersPerSecond, MinimumSeconds);
        _shown = 0f;
        _placed = false;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(Column, "modulate:a", 1f, Style.Appear).SetTrans(Tween.TransitionType.Sine);
        _tween.TweenCallback(Callable.From(() => _tween = null));
    }

    private void Left()
    {
        _tween = null;
        if (_queue.Next(out _))
        {
            Present();
        }
    }
}
