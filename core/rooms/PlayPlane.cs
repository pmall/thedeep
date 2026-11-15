namespace TheDeep;

/// <summary>Where the play plane's solids stand in depth: blocks run from <see cref="Back"/> to <see cref="Front"/>, world Z, meters.</summary>
public static class PlayPlane
{
    /// <summary>The blocks' back faces.</summary>
    public const float Back = -2f;

    /// <summary>The blocks' front faces.</summary>
    public const float Front = 2f;

    /// <summary>A section drawn on the front is kept this far off the blocks' front faces, so no face shares its plane.</summary>
    public const float SectionClear = 0.05f;

    /// <summary>Where a section (the front of a water or slag body, seen from the side) is drawn, world Z.</summary>
    public const float SectionFront = Front + SectionClear;
}
