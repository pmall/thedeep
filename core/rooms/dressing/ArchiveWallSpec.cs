namespace TheDeep;

/// <summary>The numbers of an archive wall (<see cref="ArchiveWall"/>): sizes in meters, chances 0 to 1, depths out of the wall.</summary>
public sealed record ArchiveWallSpec
{
    /// <summary>The approved look.</summary>
    public static readonly ArchiveWallSpec Default = new();

    /// <summary>A pier's width along the wall.</summary>
    public float PierWidth { get; init; } = 1.2f;

    /// <summary>How far a pier stands out of the wall.</summary>
    public float PierDepth { get; init; } = 0.4f;

    /// <summary>The room between a pier and the shelves of its bay, on each side.</summary>
    public float BayMargin { get; init; } = 0.7f;

    /// <summary>The shelves stand this far apart, up the wall.</summary>
    public float ShelfPitch { get; init; } = 3.2f;

    /// <summary>The height at the wall's top that no shelf is placed in.</summary>
    public float TopMargin { get; init; } = 0.5f;

    /// <summary>The width a server rack takes on its shelf.</summary>
    public float RackPitch { get; init; } = 1.1f;

    /// <summary>The chance a shelf above the floor has fallen, leaving bare wall.</summary>
    public float FallenShelfChance { get; init; } = 0.1f;

    /// <summary>A shelf's thickness.</summary>
    public float ShelfThickness { get; init; } = 0.18f;

    /// <summary>How far a shelf is wider than its bay's span.</summary>
    public float ShelfOverhang { get; init; } = 0.3f;

    /// <summary>How far a shelf stands out of the wall.</summary>
    public float ShelfDepth { get; init; } = 0.5f;

    /// <summary>The chance a shelf has icicles under it.</summary>
    public float IcicleChance { get; init; } = 0.6f;

    /// <summary>The icicles' width as a share of their shelf's.</summary>
    public float IcicleWidthShare { get; init; } = 0.9f;

    /// <summary>The icicles' least length.</summary>
    public float IcicleLength { get; init; } = 0.6f;

    /// <summary>What the icicles' length may add to <see cref="IcicleLength"/>.</summary>
    public float IcicleLengthSpread { get; init; } = 0.5f;

    /// <summary>How far icicles hang out of the wall.</summary>
    public float IcicleDepth { get; init; } = 0.75f;

    /// <summary>The chance a rack of a row was looted, leaving a gap.</summary>
    public float LootedChance { get; init; } = 0.18f;

    /// <summary>A rack's width as a share of its slot's.</summary>
    public float RackWidthShare { get; init; } = 0.92f;

    /// <summary>How much shorter than the shelves' pitch a rack is.</summary>
    public float RackHeightMargin { get; init; } = 0.5f;

    /// <summary>How far a rack stands out of the wall.</summary>
    public float RackDepth { get; init; } = 0.15f;

    /// <summary>How many looks a rack has.</summary>
    public int RackVariants { get; init; } = 3;
}
