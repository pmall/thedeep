namespace TheDeep;

/// <summary>Which <see cref="AnchorLook"/> an anchor shows, pure: the choice the harpoon rig makes for each anchor of the room every tick.</summary>
public static class AnchorLooks
{
    /// <summary>
    /// The look of one anchor. Without the Harpoon (<paramref name="armed"/> false) it is dormant. The anchor being fired at
    /// (<paramref name="fired"/>) is sighted until its line has bitten, then bitten; otherwise the anchor the harpoon would fire
    /// at (<paramref name="target"/>) is sighted and the one last zipped to (<paramref name="lastZipped"/>) is spent; the rest are ready
    /// or far by whether <paramref name="distance"/> (meters from the chest) is within the harpoon's reach (<see cref="HarpoonAim.InReach"/>).
    /// </summary>
    public static AnchorLook Of(bool armed, bool fired, bool bitten, bool target, bool lastZipped, float distance, float range, float minDistance)
    {
        if (!armed)
        {
            return AnchorLook.Dormant;
        }

        if (fired)
        {
            return bitten ? AnchorLook.Bitten : AnchorLook.Sighted;
        }

        if (target)
        {
            return AnchorLook.Sighted;
        }

        if (lastZipped)
        {
            return AnchorLook.Spent;
        }

        return HarpoonAim.InReach(distance, range, minDistance) ? AnchorLook.Ready : AnchorLook.Far;
    }
}
