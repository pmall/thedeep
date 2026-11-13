namespace TheDeep;

/// <summary>
/// Pure: where the lines said on screen stand, one layout for the places that share the screen, so none overlaps another or reads
/// as one block with it. The banner's high place is under the health and the room's name; the hint line stands low, one row on its
/// plate; the banner's low place (taken while the pilgrim is under the high one, <see cref="BannerPlace"/>) ends above the hint's
/// plate, a gap between. The dialogue box stands in the banner's places (<c>DialogueBox</c>). Screen pixels on the game's canvas, y down.
/// </summary>
/// <param name="BannerTop">The top of the banner's plate in its high place, below the canvas's top.</param>
/// <param name="HintBottom">The bottom of the hint's plate, above the canvas's bottom.</param>
/// <param name="Gap">Between the banner's plate in its low place and the hint's plate, at least zero.</param>
public readonly record struct TextPlaces(float BannerTop, float HintBottom, float Gap)
{
    /// <summary>The top of the hint's plate, <paramref name="hintHeight"/> tall, on a canvas <paramref name="canvasHeight"/> tall.</summary>
    public float HintTop(float canvasHeight, float hintHeight) => canvasHeight - HintBottom - hintHeight;

    /// <summary>The bottom of the banner's plate in its low place: over the hint's plate by the gap, whether a hint shows or not.</summary>
    public float BannerLowBottom(float canvasHeight, float hintHeight) => HintTop(canvasHeight, hintHeight) - System.MathF.Max(0f, Gap);
}
