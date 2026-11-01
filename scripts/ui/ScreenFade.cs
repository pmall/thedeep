using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// A full-screen veil for room transitions, respawns and the map: eases to <see cref="Color"/> and back.
/// Above the game and its HUD, under the owner capture overlay, so videos show the fades.
/// </summary>
public partial class ScreenFade : CanvasLayer
{
    /// <summary>The veil's color: the deep's darkest tint, never pure black.</summary>
    [Export] public Color Color { get; set; } = new(0.025f, 0.022f, 0.04f);

    private ColorRect? _veil;
    private ColorRect Veil => _veil ??= new ColorRect() { MouseFilter = Control.MouseFilterEnum.Ignore };
    private Tween? _tween;

    /// <summary>0 clear, 1 fully veiled.</summary>
    public float Amount => Veil.Color.A;

    public override void _Ready()
    {
        Layer = 90;
        ProcessMode = ProcessModeEnum.Always;
        Veil.Color = Color with { A = 0f };
        Veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(Veil);
    }

    /// <summary>Eases the veil to <paramref name="amount"/> over <paramref name="seconds"/>; completes when done.</summary>
    public async Task To(float amount, float seconds)
    {
        _tween?.Kill();
        if (seconds <= 0f)
        {
            Veil.Color = Color with { A = amount };
            return;
        }

        _tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _tween.TweenProperty(Veil, "color:a", amount, seconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(_tween, Tween.SignalName.Finished);
    }
}
