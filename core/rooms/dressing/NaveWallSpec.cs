namespace TheDeep;

/// <summary>The numbers of a nave wall (<see cref="NaveWall"/>): sizes in meters, shares of a span or the wall, chances 0 to 1, depths out of the wall.</summary>
public sealed record NaveWallSpec
{
    /// <summary>The approved look.</summary>
    public static readonly NaveWallSpec Default = new();

    /// <summary>The plinth's height.</summary>
    public float PlinthHeight { get; init; } = 0.8f;

    /// <summary>How far the plinth stands out of the wall.</summary>
    public float PlinthDepth { get; init; } = 0.4f;

    /// <summary>The width of an arcade pilaster.</summary>
    public float ArcadePilaster { get; init; } = 1f;

    /// <summary>The width of an upper tier's pilaster.</summary>
    public float UpperPilaster { get; init; } = 0.6f;

    /// <summary>The height of the block a shaft carries.</summary>
    public float CapitalHeight { get; init; } = 0.4f;

    /// <summary>How far a capital is wider than its shaft.</summary>
    public float CapitalOverhang { get; init; } = 0.4f;

    /// <summary>How far a capital stands out of the wall.</summary>
    public float CapitalDepth { get; init; } = 0.4f;

    /// <summary>How far a shaft stands out of the wall.</summary>
    public float ShaftDepth { get; init; } = 0.25f;

    /// <summary>The height of a string course.</summary>
    public float CourseHeight { get; init; } = 0.45f;

    /// <summary>How far a course stands out of the wall.</summary>
    public float CourseDepth { get; init; } = 0.35f;

    /// <summary>The thickness of an arch's ring, added to the arcade's span.</summary>
    public float ArchRing { get; init; } = 0.45f;

    /// <summary>The arcade's arch rise as a share of its span (over half, so it is pointed).</summary>
    public float ArcadeRiseShare { get; init; } = 0.62f;

    /// <summary>The arcade's shaft height is the wall's times this, at most.</summary>
    public float ArcadeShaftShare { get; init; } = 0.32f;

    /// <summary>The room the arcade leaves at the wall's top.</summary>
    public float TopMargin { get; init; } = 0.5f;

    /// <summary>The arcade shaft's least height.</summary>
    public float ShaftMin { get; init; } = 2f;

    /// <summary>The arcade shaft's greatest height.</summary>
    public float ShaftMax { get; init; } = 6.5f;

    /// <summary>A tier that overshoots the wall's top by no more than this still stands.</summary>
    public float FitTolerance { get; init; } = 1e-3f;

    /// <summary>How far a blind arch's recess stands back (the least depth in the wall).</summary>
    public float RecessDepth { get; init; } = 0.04f;

    /// <summary>The chance an arcade's arch has fallen.</summary>
    public float FallenArchChance { get; init; } = 0.08f;

    /// <summary>How far an arch stands out of the wall.</summary>
    public float ArchDepth { get; init; } = 0.25f;

    /// <summary>The chance a tympanum has a rose.</summary>
    public float RoseChance { get; init; } = 0.5f;

    /// <summary>A rose's radius as a share of its bay's inner width, at most.</summary>
    public float RoseWidthShare { get; init; } = 0.16f;

    /// <summary>A rose's radius as a share of its arch's rise, at most.</summary>
    public float RoseRiseShare { get; init; } = 0.22f;

    /// <summary>The least radius a rose is worth.</summary>
    public float RoseMinRadius { get; init; } = 0.35f;

    /// <summary>Where a rose sits up its arch's rise, as a share.</summary>
    public float RoseHeightShare { get; init; } = 0.45f;

    /// <summary>How far a rose stands out of the wall.</summary>
    public float RoseDepth { get; init; } = 0.15f;

    /// <summary>A bay with a wider inner width than this holds three lancets.</summary>
    public float ThreeLancetsOver { get; init; } = 5f;

    /// <summary>A bay with a wider inner width than this (and not three) holds two lancets.</summary>
    public float TwoLancetsOver { get; init; } = 2.6f;

    /// <summary>A lancet's greatest width.</summary>
    public float LancetMax { get; init; } = 1.3f;

    /// <summary>The inner width a lancet is given, as a share of its width, counting the room around it.</summary>
    public float LancetRoom { get; init; } = 1.9f;

    /// <summary>The chance a lancet is walled up.</summary>
    public float WalledUpChance { get; init; } = 0.12f;

    /// <summary>A lancet's jambs' width.</summary>
    public float Jamb { get; init; } = 0.18f;

    /// <summary>How far a lancet's recess is lifted over its tier's middle.</summary>
    public float LancetLift { get; init; } = 0.2f;

    /// <summary>How much shorter than its tier a lancet is, and how far its jambs start above the tier's foot.</summary>
    public float LancetInset { get; init; } = 0.4f;

    /// <summary>How far a lancet's jambs and arch stand out of the wall.</summary>
    public float LancetDepth { get; init; } = 0.15f;

    /// <summary>A lancet's arch rise as a share of its width (with the jambs).</summary>
    public float LancetRiseShare { get; init; } = 0.75f;

    /// <summary>An upper tier's shaft height is the wall's times this, within <see cref="UpperShaftMin"/> and <see cref="UpperShaftMax"/>.</summary>
    public float UpperShaftShare { get; init; } = 0.18f;

    /// <summary>The upper shaft's least height.</summary>
    public float UpperShaftMin { get; init; } = 2.4f;

    /// <summary>The upper shaft's greatest height.</summary>
    public float UpperShaftMax { get; init; } = 4f;

    /// <summary>An upper tier's arch spans at most this wide.</summary>
    public float UpperSpanMax { get; init; } = 3f;

    /// <summary>An upper tier's arch rise.</summary>
    public float UpperRise { get; init; } = 1.2f;

    /// <summary>How far an icicle fringe stands out of the wall.</summary>
    public float IcicleDepth { get; init; } = 0.6f;

    /// <summary>An icicle fringe's width as a share of its bay's inner width.</summary>
    public float IcicleWidthShare { get; init; } = 0.85f;

    /// <summary>An icicle fringe's least length.</summary>
    public float IcicleLength { get; init; } = 0.5f;

    /// <summary>What an icicle fringe's length may add to <see cref="IcicleLength"/>.</summary>
    public float IcicleLengthSpread { get; init; } = 0.6f;

    /// <summary>The bevel of the plinth's block (its depth is twice <see cref="PlinthDepth"/>: the block is centered on it).</summary>
    public float PlinthBevel { get; init; } = 0.08f;

    /// <summary>A shaft's bevel, at most (a slim shaft takes <see cref="ShaftBevelShare"/> of its width).</summary>
    public float ShaftBevel { get; init; } = 0.06f;

    /// <summary>A slim shaft's bevel as a share of its width.</summary>
    public float ShaftBevelShare { get; init; } = 0.2f;

    /// <summary>A shaft wider than this has a fillet down its face.</summary>
    public float FilletMinWidth { get; init; } = 0.4f;

    /// <summary>A fillet's width as a share of its shaft's...</summary>
    public float FilletShare { get; init; } = 0.4f;

    /// <summary>...this deep...</summary>
    public float FilletDepth { get; init; } = 0.2f;

    /// <summary>...with this bevel...</summary>
    public float FilletBevel { get; init; } = 0.04f;

    /// <summary>...and standing this far out of the wall.</summary>
    public float FilletOffset { get; init; } = 0.3f;

    /// <summary>The bevel of a capital's block (its depth is twice <see cref="CapitalDepth"/>).</summary>
    public float CapitalBevel { get; init; } = 0.06f;

    /// <summary>An arch's ring is at most this share of its span thick (and <see cref="ArchRing"/>).</summary>
    public float ArchRingShare { get; init; } = 0.12f;

    /// <summary>An arch's ring is this deep.</summary>
    public float ArchMeshDepth { get; init; } = 0.45f;

    /// <summary>An arch's ring is cut in this many segments.</summary>
    public int ArchSegments { get; init; } = 24;

    /// <summary>A recess's block is this deep.</summary>
    public float RecessMeshDepth { get; init; } = 0.06f;

    /// <summary>The bevel of a recess's block.</summary>
    public float RecessBevel { get; init; } = 0.01f;

    /// <summary>The bevel of a course's block (its depth is twice <see cref="CourseDepth"/>).</summary>
    public float CourseBevel { get; init; } = 0.08f;

    /// <summary>The shadowed back of a rose is this thick.</summary>
    public float RoseBackHeight { get; init; } = 0.06f;

    /// <summary>A rose's ring is this deep.</summary>
    public float RoseRingHeight { get; init; } = 0.35f;

    /// <summary>A rose's ring is this share of its radius thick.</summary>
    public float RoseRingShare { get; init; } = 0.16f;

    /// <summary>A rose's rings are cut in this many segments.</summary>
    public int RoseSegments { get; init; } = 32;

    /// <summary>A rose has this many bars of tracery, evenly turned.</summary>
    public int TraceryCount { get; init; } = 4;

    /// <summary>A tracery bar is this share of the rose's diameter long...</summary>
    public float TraceryWidthShare { get; init; } = 0.9f;

    /// <summary>...this thick...</summary>
    public float TraceryThickness { get; init; } = 0.1f;

    /// <summary>...this deep...</summary>
    public float TraceryDepth { get; init; } = 0.2f;

    /// <summary>...with this bevel.</summary>
    public float TraceryBevel { get; init; } = 0.02f;
}
