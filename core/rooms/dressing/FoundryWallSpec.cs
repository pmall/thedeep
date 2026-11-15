namespace TheDeep;

/// <summary>The numbers of a foundry wall (<see cref="FoundryWall"/>): sizes in meters, shares of the wall or bay, chances 0 to 1, depths out of the wall.</summary>
public sealed record FoundryWallSpec
{
    /// <summary>The approved look.</summary>
    public static readonly FoundryWallSpec Default = new();

    /// <summary>A pilaster's width along the wall.</summary>
    public float PilasterWidth { get; init; } = 0.9f;

    /// <summary>How far a pilaster stands out of the wall.</summary>
    public float PilasterDepth { get; init; } = 0.35f;

    /// <summary>The lower girder's height is the wall's height times this, but at most <see cref="LowGirderMax"/>.</summary>
    public float LowGirderShare { get; init; } = 0.4f;

    /// <summary>The lower girder's greatest height up the wall.</summary>
    public float LowGirderMax { get; init; } = 6f;

    /// <summary>A wall taller than this also gets a high girder.</summary>
    public float TallWall { get; init; } = 14f;

    /// <summary>The high girder's height as a share of the wall's.</summary>
    public float HighGirderShare { get; init; } = 0.72f;

    /// <summary>A girder's thickness.</summary>
    public float GirderThickness { get; init; } = 0.6f;

    /// <summary>How far a girder stands out of the wall.</summary>
    public float GirderDepth { get; init; } = 0.55f;

    /// <summary>The chance a bay (wide enough) is a furnace mouth; the pipes take the bays up to <see cref="PipeBelow"/>.</summary>
    public float MouthChance { get; init; } = 0.4f;

    /// <summary>A bay's motif roll under this (and over <see cref="MouthChance"/>, or too narrow for a mouth) is pipes.</summary>
    public float PipeBelow { get; init; } = 0.75f;

    /// <summary>A bay narrower than this gets no furnace mouth.</summary>
    public float MouthMinSpan { get; init; } = 3f;

    /// <summary>A mouth's width as a share of its bay.</summary>
    public float MouthSpanShare { get; init; } = 0.6f;

    /// <summary>A mouth's greatest width.</summary>
    public float MouthMaxWidth { get; init; } = 4f;

    /// <summary>A mouth's height as a share of its width.</summary>
    public float MouthAspect { get; init; } = 0.7f;

    /// <summary>The least height of a mouth that fits under the wall's top.</summary>
    public float MouthMinHeight { get; init; } = 1.5f;

    /// <summary>The room a mouth leaves between its top and the wall's top.</summary>
    public float MouthHeadroom { get; init; } = 2f;

    /// <summary>How far a mouth stands over the girder.</summary>
    public float MouthLift { get; init; } = 0.4f;

    /// <summary>How far back the glow stands in the wall.</summary>
    public float GlowDepth { get; init; } = 0.05f;

    /// <summary>How far the grate stands out of the wall.</summary>
    public float MouthDepth { get; init; } = 0.2f;

    /// <summary>The most pipes a bay carries.</summary>
    public int MaxPipes { get; init; } = 2;

    /// <summary>A pipe's least radius.</summary>
    public float PipeRadius { get; init; } = 0.28f;

    /// <summary>What a pipe's radius may add to <see cref="PipeRadius"/>.</summary>
    public float PipeRadiusSpread { get; init; } = 0.12f;

    /// <summary>How far a pipe stands out of the wall.</summary>
    public float PipeDepth { get; init; } = 0.4f;

    /// <summary>The chance a bay has a chain hanging from the top.</summary>
    public float ChainChance { get; init; } = 0.45f;

    /// <summary>A chain's least length as a share of the wall's height.</summary>
    public float ChainLengthShare { get; init; } = 0.25f;

    /// <summary>What a chain's length may add to <see cref="ChainLengthShare"/>, as a share of the wall's height.</summary>
    public float ChainLengthSpread { get; init; } = 0.45f;

    /// <summary>Where along its bay a chain hangs, at least, as a share of the bay.</summary>
    public float ChainAt { get; init; } = 0.3f;

    /// <summary>What a chain's place may add to <see cref="ChainAt"/>, as a share of the bay.</summary>
    public float ChainAtSpread { get; init; } = 0.4f;

    /// <summary>A chain links' thickness.</summary>
    public float ChainWidth { get; init; } = 0.07f;

    /// <summary>How far a chain hangs out of the wall.</summary>
    public float ChainDepth { get; init; } = 0.9f;
}
