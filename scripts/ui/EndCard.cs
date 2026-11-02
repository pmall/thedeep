using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// The end of the game, a placeholder until the final touch (docs/roadmap.md#stage-polish): the screen fades to a warm black, a title
/// and two lines ease in one after another, hold, and fade. Text comes from the line book (ending.*).
/// </summary>
public partial class EndCard : CanvasLayer
{
    [Export] public float FadeTime { get; set; } = 2.5f;
    [Export] public float Hold { get; set; } = 6f;

    private ColorRect? _veil;
    private ColorRect Veil => _veil ??= new ColorRect { Color = new Color(0.03f, 0.012f, 0.01f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
    private Label? _title;
    private Label TitleLabel => _title ??= MakeLabel(54, new Color(1f, 0.86f, 0.78f));
    private Label? _line;
    private Label LineLabel => _line ??= MakeLabel(24, new Color(0.92f, 0.86f, 0.84f));
    private Label? _thanks;
    private Label ThanksLabel => _thanks ??= MakeLabel(20, new Color(0.8f, 0.72f, 0.7f));

    public override void _Ready()
    {
        Layer = 90;
        ProcessMode = ProcessModeEnum.Always;
        Veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(Veil);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 26);
        box.AddChild(TitleLabel);
        box.AddChild(LineLabel);
        box.AddChild(ThanksLabel);
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
        TitleLabel.Text = book.Find(GameLines.EndingTitle)?.Text ?? "";
        LineLabel.Text = book.Find(GameLines.EndingLine)?.Text ?? "";
        ThanksLabel.Text = book.Find(GameLines.EndingThanks)?.Text ?? "";
        Visible = true;
        Tween tween = CreateTween();
        tween.TweenProperty(Veil, "color:a", 1f, FadeTime).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(TitleLabel, "modulate:a", 1f, 1.4f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(LineLabel, "modulate:a", 1f, 1.4f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenInterval(Hold * 0.5f);
        tween.TweenProperty(ThanksLabel, "modulate:a", 1f, 1.4f).SetTrans(Tween.TransitionType.Sine);
        tween.TweenInterval(Hold * 0.5f);
        tween.TweenProperty(TitleLabel, "modulate:a", 0f, 1.6f);
        tween.Parallel().TweenProperty(LineLabel, "modulate:a", 0f, 1.6f);
        tween.Parallel().TweenProperty(ThanksLabel, "modulate:a", 0f, 1.6f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
