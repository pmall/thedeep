using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// A title and a line in the lower third of the screen, eased in, held, eased out: an ability taken.
/// Text always comes from the line book (ids), never from code.
/// </summary>
public partial class Banner : CanvasLayer
{
    private Label _title = null!;
    private Label _line = null!;
    private Control _box = null!;
    private Tween? _tween;

    public override void _Ready()
    {
        Layer = 75;
        _box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _box.OffsetTop = 520f;
        _title = MakeLabel(40, new Color(0.95f, 0.93f, 1f));
        _line = MakeLabel(20, new Color(0.85f, 0.83f, 0.92f));
        _box.AddChild(_title);
        _box.AddChild(_line);
        _box.Modulate = Colors.White with { A = 0f };
        AddChild(_box);
    }

    private static Label MakeLabel(int size, Color color)
    {
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.6f));
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    /// <summary>Shows the lines <paramref name="titleId"/> and <paramref name="lineId"/> for <paramref name="hold"/> seconds.</summary>
    public async Task Show(StringName titleId, StringName lineId, float hold = 3.5f)
    {
        var book = LineBook.Shared;
        _title.Text = book.Find(titleId)?.Text ?? "";
        _line.Text = book.Find(lineId)?.Text ?? "";
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(_box, "modulate:a", 1f, 0.7f).SetTrans(Tween.TransitionType.Sine);
        _tween.TweenInterval(hold);
        _tween.TweenProperty(_box, "modulate:a", 0f, 1.0f).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(_tween, Tween.SignalName.Finished);
    }
}
