using Godot;

namespace TheDeep;

/// <summary>
/// One quiet line low on the screen, with a small mark in a gate's color: what the pilgrim says meeting a gate it can't pass yet
/// (<see cref="GateHints"/>). Eased in, held, eased out; a new line replaces the one showing. Text comes from the line book.
/// </summary>
public partial class HintLine : CanvasLayer
{
    [Export] public float Hold { get; set; } = 3.2f;

    private Label _label = null!;
    private Control _mark = null!;
    private Control _box = null!;
    private Color _color = Colors.White;
    private Tween? _tween;

    public override void _Ready()
    {
        Layer = 74;
        _box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _box.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _box.OffsetTop = -132f;
        _box.OffsetBottom = -92f;
        _box.AddThemeConstantOverride("separation", 14);
        _mark = new Control { CustomMinimumSize = new Vector2(14f, 14f), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _mark.Draw += () => _mark.DrawCircle(_mark.Size * 0.5f, 5.5f, _color);
        _label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _label.AddThemeFontSizeOverride("font_size", 22);
        _label.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.95f));
        _label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.65f));
        _label.AddThemeConstantOverride("shadow_offset_y", 2);
        _box.AddChild(_mark);
        _box.AddChild(_label);
        _box.Modulate = Colors.White with { A = 0f };
        AddChild(_box);
    }

    /// <summary>Whether a line is showing (or easing).</summary>
    public bool Showing => _tween is not null && _tween.IsRunning();

    /// <summary>Shows the line <paramref name="lineId"/> with a mark in <paramref name="color"/>.</summary>
    public void Show(StringName lineId, Color color)
    {
        _label.Text = LineBook.Shared.Find(lineId)?.Text ?? "";
        _color = color;
        _mark.QueueRedraw();
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(_box, "modulate:a", 1f, 0.45f).SetTrans(Tween.TransitionType.Sine);
        _tween.TweenInterval(Hold);
        _tween.TweenProperty(_box, "modulate:a", 0f, 0.9f).SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>Gone at once (a room left under the veil).</summary>
    public void Clear()
    {
        if (!IsNodeReady())
        {
            return;
        }

        _tween?.Kill();
        _tween = null;
        _box.Modulate = Colors.White with { A = 0f };
    }
}
