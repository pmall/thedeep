namespace TheDeep;

/// <summary>The one burn rule both heat and slag apply (`docs/gameplay.md#slag`, `docs/gameplay.md#heat`), pure.</summary>
public static class Burn
{
    /// <summary>
    /// The space around <paramref name="burning"/> whose ground is no footing: <paramref name="margin"/> on its sides and under it,
    /// <paramref name="above"/> over it (slag's reaches up the banks; heat's is the margin all round).
    /// </summary>
    public static Box KeepOff(Box burning, float margin, float above) =>
        burning.Grown(margin) with { MaxY = burning.MaxY + above };

    /// <summary>
    /// Where <paramref name="body"/> stands against the <paramref name="burning"/> region: touching when they share area (boxes
    /// that only meet along an edge don't), near inside <paramref name="keepOff"/> (the space around it whose ground is no footing),
    /// clear otherwise.
    /// </summary>
    public static BurnReach Reach(Box burning, Box keepOff, Box body) =>
        body.Overlaps(burning) ? BurnReach.Touching : body.Overlaps(keepOff) ? BurnReach.Near : BurnReach.Clear;
}
