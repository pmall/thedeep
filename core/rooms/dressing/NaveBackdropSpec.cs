namespace TheDeep;

/// <summary>
/// The numbers of a nave backdrop (<see cref="NaveBackdrop"/>): sizes in meters per unit of the row's scale (a share is of
/// the row's pitch, as its name says), chances 0 to 1.
/// </summary>
public sealed record NaveBackdropSpec
{
    /// <summary>The approved look.</summary>
    public static readonly NaveBackdropSpec Default = new();

    /// <summary>A pier's height, from the row's base to where its arch springs.</summary>
    public float PierHeight { get; init; } = 9f;

    /// <summary>A pier's radius.</summary>
    public float PierRadius { get; init; } = 0.9f;

    /// <summary>The height of an upper tier's pier, slimmer and shorter than the first tier's.</summary>
    public float UpperHeight { get; init; } = 7.5f;

    /// <summary>How far a first-tier arch rises above its springing line, as a share of the pitch; over half, so it is pointed.</summary>
    public float PointedRiseShare { get; init; } = 0.62f;

    /// <summary>The upper tiers' arches rise, as a share of the pitch: pointed too (round ones stacked read as a lattice of rings), a little lower.</summary>
    public float UpperRiseShare { get; init; } = 0.56f;

    /// <summary>The height of the stone course that runs over a tier's arches.</summary>
    public float CourseHeight { get; init; } = 0.7f;

    /// <summary>The thickness of an arch's ring, in the plane of the row.</summary>
    public float ArchThickness { get; init; } = 0.8f;

    /// <summary>An arch's depth toward the camera.</summary>
    public float ArchDepth { get; init; } = 1.6f;

    /// <summary>The side of a shaft's square section up a pier's face.</summary>
    public float ShaftSection { get; init; } = 0.4f;

    /// <summary>The chance a pier is broken off below its top tier.</summary>
    public float BrokenPierChance { get; init; } = 0.2f;

    /// <summary>The chance a tall pier has a shaft up its face.</summary>
    public float ShaftChance { get; init; } = 0.2f;

    /// <summary>The chance an arch has fallen.</summary>
    public float FallenArchChance { get; init; } = 0.1f;
}
