using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// The end of the game, a placeholder until the final touch (roadmap, phase 5): the screen fades to a warm black, a title
/// and two lines ease in one after another, hold, and fade. Text comes from the line book (ending.*).
/// </summary>
public partial class EndCard : CanvasLayer
{
    [Export] public float FadeTime { get; set; } = 2.5f;
    [Export] public float Hold { get; set; } = 6f;

    private ColorRect _veil = null!;
    private Label _title = null!;
    private Label _line = null!;
    private Label _thanks = null!;

    public override void _Ready()
    {
        Layer = 90;
        ProcessMode = ProcessModeEnum.Always;
        _veil = new ColorRect { Color = new Color(0.03f, 0.012f, 0.01f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_veil);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 26);
        _title = MakeLabel(54, new Color(1f, 0.86f, 0.78f));
        _line = MakeLabel(24, new Color(0.92f, 0.86f, 0.84f));
        _thanks = MakeLabel(20, new Color(0.8f, 0.72f, 0.7f));
        box.AddChild(_title);
        box.AddChild(_line);
        box.AddChild(_thanks);
        AddChild(box);
        Visible = false;
    }

    private static Label MakeLabel(int size, Color color)
    {
        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = Colors.White with { A = 0f },
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>Plays the card to its end (the veil stays down).</summary>
    public async Task Play()
    {
        LineBook book = LineBook.Shared;
        _title.Text = book.Find("ending.title")?.Text ?? "";
        _line.Text = book.Find("ending.line")?.Text ?? "";
        _thanks.Text = book.Find("ending.thanks")?.Text ?? "";
        Visible = true;
        Tween tween = CreateTween();
        tween.TweenProperty(_veil, "color:a", 1f, FadeTime).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(_title, "modulate:a", 1f, 1.4f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(_line, "modulate:a", 1f, 1.4f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenInterval(Hold * 0.5f);
        tween.TweenProperty(_thanks, "modulate:a", 1f, 1.4f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenInterval(Hold * 0.5f);
        tween.TweenProperty(_title, "modulate:a", 0f, 1.6f);
        tween.Parallel().TweenProperty(_line, "modulate:a", 0f, 1.6f);
        tween.Parallel().TweenProperty(_thanks, "modulate:a", 0f, 1.6f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
