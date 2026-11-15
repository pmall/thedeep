namespace TheDeep;

/// <summary>
/// The draw order of the game's canvas layers, lowest first, in one place. The owner capture (layer 100, scenes/ui/OwnerCapture.tscn)
/// sits above all of them so videos show the fades; the end card is over the fade because it plays while the veil is down.
/// </summary>
public static class UiLayers
{
    /// <summary>The chevron pointing at an anchor off screen.</summary>
    public const int AnchorPointer = 69;

    /// <summary>The pilgrim's health.</summary>
    public const int Health = 70;

    /// <summary>The action prompts.</summary>
    public const int ActionPrompt = 72;

    /// <summary>The hint line.</summary>
    public const int HintLine = 74;

    /// <summary>The banner announcing a relic, a power-up or a room.</summary>
    public const int Banner = 75;

    /// <summary>The dialogue box.</summary>
    public const int Dialogue = 76;

    /// <summary>The map, over the HUD it pauses.</summary>
    public const int Map = 80;

    /// <summary>The veil of fades.</summary>
    public const int Fade = 90;

    /// <summary>The end card, over the veil.</summary>
    public const int EndCard = 91;
}
