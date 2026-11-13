namespace TheDeep;

/// <summary>
/// Pure: whether the banner stands in its low place rather than its high one, so the pilgrim is never under it. It stands high, clear
/// of the play around the pilgrim, which the camera frames a little below the screen's middle; where the camera stops at a room's top
/// and the pilgrim climbs into the high place, the banner goes low, and comes back only once the pilgrim is clear of the high place
/// by a margin, so a pilgrim on its edge doesn't send it back and forth. Screen pixels, y down.
/// </summary>
public static class BannerPlace
{
    /// <summary>
    /// Whether the banner stands low, given the top of the pilgrim's body on screen (<paramref name="pilgrimTop"/>), the bottom of
    /// the banner's high place (<paramref name="highBottom"/>), the margin the pilgrim must leave below it before the banner comes back
    /// (<paramref name="clearance"/>), and whether it stands low now (<paramref name="low"/>).
    /// </summary>
    public static bool Low(float pilgrimTop, float highBottom, float clearance, bool low) =>
        pilgrimTop < highBottom + (low ? clearance : 0f);
}
