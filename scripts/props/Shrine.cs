using Godot;

namespace TheDeep;

/// <summary>
/// A pilgrim shrine: a niche with glowing faith glyphs where the pilgrim rests. Resting (interact, when close)
/// saves the game and restores health; dying brings the pilgrim back to the last shrine rested at
/// (<see cref="Session"/>). The node is where the pilgrim stands to rest, on the floor in front of the niche.
/// </summary>
public partial class Shrine : Node3D
{
    /// <summary>The glyphs' light: takes the biome's glyph color (the room lists it in its GlyphLights).</summary>
    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>The prompt shown when the pilgrim can rest (its text is the line ui.rest).</summary>
    [Export] public Label3D Prompt { get; set; } = null!;

    private float _promptShown;
    private bool _promptWanted;
    private float _baseEnergy;
    private Tween? _flare;

    public override void _Ready()
    {
        _baseEnergy = Light.LightEnergy;
        Prompt.Text = LineBook.Shared.Find("ui.rest")?.Text ?? "";
        Prompt.Modulate = Prompt.Modulate with { A = 0f };
    }

    /// <summary>Shows or hides the prompt (eased).</summary>
    public void ShowPrompt(bool shown) => _promptWanted = shown;

    public override void _Process(double delta)
    {
        _promptShown = Motion.Approach(_promptShown, _promptWanted ? 1f : 0f, (float)delta * 4f);
        Prompt.Modulate = Prompt.Modulate with { A = _promptShown * _promptShown * (3f - 2f * _promptShown) * 0.85f };
        Prompt.Position = Prompt.Position with { Y = 2.6f + 0.08f * _promptShown };
    }

    /// <summary>The glyphs flare and settle back: the pilgrim has rested.</summary>
    public void Flare()
    {
        _flare?.Kill();
        _flare = CreateTween();
        _flare.TweenProperty(Light, "light_energy", _baseEnergy * 4f, 0.25f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _flare.TweenProperty(Light, "light_energy", _baseEnergy, 1.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }
}
