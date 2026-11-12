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

    /// <summary>The prompt offered while the pilgrim can rest ("Rest").</summary>
    [Export] public ActionPrompt Prompt { get; set; } = null!;

    private float _baseEnergy;
    private Tween? _flare;

    public override void _Ready()
    {
        _baseEnergy = Light.LightEnergy;
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
