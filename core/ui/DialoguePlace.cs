namespace TheDeep;

/// <summary>
/// Pure: where the dialogue box stands while a conversation plays, so neither the pilgrim nor the one speaking is under it. Its
/// places are the banner's (<see cref="TextPlaces"/>): high, under the health and the room's name, by default; low, over the hint
/// line, when the high place would cover either of them and the low one covers neither; when both places cover one of them, the
/// place clear of the pilgrim. Rectangles on the screen, pixels, y down.
/// </summary>
public static class DialoguePlace
{
    /// <summary>
    /// Whether the box stands in its low place (<paramref name="low"/>) rather than its high one (<paramref name="high"/>), given the
    /// pilgrim's body on screen (<paramref name="pilgrim"/>) and the speaker's (<paramref name="speaker"/>).
    /// </summary>
    public static bool Low(Box high, Box low, Box pilgrim, Box speaker)
    {
        bool highClear = !high.Overlaps(pilgrim) && !high.Overlaps(speaker);
        if (highClear)
        {
            return false;
        }

        bool lowClear = !low.Overlaps(pilgrim) && !low.Overlaps(speaker);
        return lowClear || high.Overlaps(pilgrim) && !low.Overlaps(pilgrim);
    }
}
