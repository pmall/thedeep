using Godot;

namespace TheDeep;

/// <summary>
/// One quiet line low on the screen, with a small mark in a gate's color: what the pilgrim says meeting a gate it can't pass yet
/// (<see cref="GateHints"/>, <see cref="Keyring"/>). Eased in, held, eased out; a line said while another shows is read after it
/// (<see cref="LinesInTurn{T}"/>, docs/gameplay.md#gate-says), sooner than a line alone would leave. Text comes from the line book.
/// </summary>
public partial class HintLine : CanvasLayer
{
    [Export] public float Hold { get; set; } = 3.2f;

    /// <summary>Seconds a line holds when another waits behind it.</summary>
    [Export] public float HoldWhenWaited { get; set; } = 2.4f;

    /// <summary>How many lines may wait behind the one showing.</summary>
    public const int Waits = 2;

    private readonly LinesInTurn<(StringName Line, Color Color)> _queue = new(Waits);
    private float _shown;

    private Label? _label;
    private Label Words => _label ??= new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
    private Control? _mark;
    private Control Mark => _mark ??= new Control { CustomMinimumSize = new Vector2(14f, 14f), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
    private Control? _box;
    private Control Box => _box ??= new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
    private Color _color = Colors.White;
    private Tween? _tween;

    public override void _Ready()
    {
        Layer = 74;
        Box.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        Box.OffsetTop = -132f;
        Box.OffsetBottom = -92f;
        Box.AddThemeConstantOverride("separation", 14);
        Mark.Draw += () => Mark.DrawCircle(Mark.Size * 0.5f, 5.5f, _color);
        Words.AddThemeFontSizeOverride("font_size", 22);
        Words.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.95f));
        Words.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.65f));
        Words.AddThemeConstantOverride("shadow_offset_y", 2);
        Box.AddChild(Mark);
        Box.AddChild(Words);
        Box.Modulate = Colors.White with { A = 0f };
        AddChild(Box);
    }

    /// <summary>Whether a line is showing (or easing).</summary>
    public bool Showing => _queue.Showing;

    /// <summary>The line showing, or null.</summary>
    public StringName? Current => _queue.Showing ? _queue.Current.Line : null;

    /// <summary>How many lines wait behind the one showing.</summary>
    public int Waiting => _queue.Waiting;

    /// <summary>Says the line <paramref name="lineId"/> with a mark in <paramref name="color"/>: now, or after the lines already said.</summary>
    public void Show(StringName lineId, Color color)
    {
        if (_queue.Offer((lineId, color)))
        {
            Present();
        }
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
            _tween.TweenProperty(Box, "modulate:a", 0f, _queue.Waiting > 0 ? 0.35f : 0.9f).SetTrans(Tween.TransitionType.Sine);
            _tween.TweenCallback(Callable.From(Left));
        }
    }

    private void Present()
    {
        (StringName line, Color color) = _queue.Current;
        Words.Text = LineBook.Shared.Find(line)?.Text ?? "";
        _color = color;
        Mark.QueueRedraw();
        _shown = 0f;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(Box, "modulate:a", 1f, 0.45f).SetTrans(Tween.TransitionType.Sine);
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
        if (!IsNodeReady())
        {
            return;
        }

        _tween?.Kill();
        _tween = null;
        Box.Modulate = Colors.White with { A = 0f };
    }
}
