using Godot;

namespace TheDeep;

/// <summary>
/// One quiet line low on the screen, on the style's plate so it reads over the palest wall and the darkest, with a mark in a gate's
/// color beside it, large enough to tell the color at a glance: what the pilgrim says meeting a gate it can't pass yet
/// (<see cref="GateHints"/>, <see cref="Keyring"/>). Eased in, held, eased out; a line said while another shows is read after it
/// (<see cref="LinesInTurn{T}"/>, docs/gameplay.md#gate-says), sooner than a line alone would leave; a line whose reason is gone
/// (a law's refusal, the law broken) is withdrawn at once (<see cref="Withdraw"/>). It stands one row tall where the style's layout
/// puts it, under the banner's low place (<see cref="TextStyle.Places"/>). Text comes from the line book, written in the shared
/// <see cref="TextStyle"/>.
/// </summary>
public partial class HintLine : CanvasLayer
{
    [Export] public float Hold { get; set; } = 3.2f;

    /// <summary>Seconds a line holds when another waits behind it.</summary>
    [Export] public float HoldWhenWaited { get; set; } = 2.4f;

    /// <summary>The gate's mark: its width across, pixels (a disc, at most the row's height).</summary>
    [Export] public float MarkSize { get; set; } = 16f;

    /// <summary>How many lines may wait behind the one showing.</summary>
    public const int Waits = 2;

    private readonly LinesInTurn<(StringName Line, Color Color)> _queue = new(Waits);
    private float _shown;
    private bool _withdrawn;

    private Label? _label;
    private Label Words => _label ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
    private Control? _mark;
    private Control Mark => _mark ??= new Control { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
    private PanelContainer? _plate;
    private PanelContainer Plate => _plate ??= new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
    private HBoxContainer? _row;
    private HBoxContainer Row => _row ??= new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
    private Color _color = Colors.White;
    private Tween? _tween;

    private static TextStyle Style => TextStyle.Shared;

    public override void _Ready()
    {
        Layer = 74;
        Row.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        Mark.CustomMinimumSize = new Vector2(MarkSize, MarkSize);
        Mark.Draw += () => Mark.DrawCircle(Mark.Size * 0.5f, MarkSize * 0.5f, _color);
        Style.Dress(Words, Style.Body, Style.Ink);
        var words = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        words.AddThemeConstantOverride("separation", Mathf.RoundToInt(Style.Padding.Y));
        words.AddChild(Mark);
        words.AddChild(Words);
        Plate.AddThemeStyleboxOverride("panel", Style.NewPlate());
        Plate.AddChild(words);
        Row.AddChild(Plate);
        Row.Modulate = Colors.White with { A = 0f };
        AddChild(Row);
    }

    /// <summary>Whether a line is showing (or easing in or out).</summary>
    public bool Showing => _queue.Showing || _withdrawn;

    /// <summary>The line showing, or null (none, or the one showing withdrawn and easing out).</summary>
    public StringName? Current => _queue.Showing && !_withdrawn ? _queue.Current.Line : null;

    /// <summary>How many lines wait behind the one showing.</summary>
    public int Waiting => _queue.Waiting;

    /// <summary>Says the line <paramref name="lineId"/> with a mark in <paramref name="color"/>: now, or after the lines already said.</summary>
    public void Show(StringName lineId, Color color)
    {
        if (_queue.Offer((lineId, color)) && !_withdrawn)
        {
            Present();
        }
    }

    /// <summary>
    /// Withdraws the line <paramref name="lineId"/> said with <paramref name="color"/>, its reason gone: showing, it eases out at once
    /// (the next waiting read after it); waiting, it is never read.
    /// </summary>
    public void Withdraw(StringName lineId, Color color)
    {
        if (!_queue.Withdraw((lineId, color)) || !IsNodeReady())
        {
            return;
        }

        _withdrawn = true;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(Row, "modulate:a", 0f, Style.Hurried).SetTrans(Tween.TransitionType.Sine);
        _tween.TweenCallback(Callable.From(() =>
        {
            _tween = null;
            _withdrawn = false;
            if (_queue.Showing)
            {
                Present();
            }
        }));
    }

    /// <summary>The line showing leaves once it has been held long enough: sooner when another waits.</summary>
    public override void _Process(double delta)
    {
        if (!_queue.Showing || _tween is not null)
        {
            return;
        }

        _shown += (float)delta;
        if (_shown >= (_queue.Waiting > 0 ? HoldWhenWaited : Hold))
        {
            _tween = CreateTween();
            _tween.TweenProperty(Row, "modulate:a", 0f, _queue.Waiting > 0 ? Style.Hurried : Style.Leave).SetTrans(Tween.TransitionType.Sine);
            _tween.TweenCallback(Callable.From(Left));
        }
    }

    private void Present()
    {
        (StringName line, Color color) = _queue.Current;
        Words.Text = LineBook.Shared.Find(line)?.Text ?? "";
        _color = color;
        Mark.QueueRedraw();
        float row = Style.RowPlate(Style.Body);
        Row.Position = new Vector2(0f, Style.Places.HintTop(GetViewport().GetVisibleRect().Size.Y, row));
        Plate.CustomMinimumSize = new Vector2(0f, row);
        Plate.ResetSize();
        _shown = 0f;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(Row, "modulate:a", 1f, Style.Appear).SetTrans(Tween.TransitionType.Sine);
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

    /// <summary>Gone at once (a room left under the veil).</summary>
    public void Clear()
    {
        _queue.Clear();
        _withdrawn = false;
        if (!IsNodeReady())
        {
            return;
        }

        _tween?.Kill();
        _tween = null;
        Row.Modulate = Colors.White with { A = 0f };
    }
}
