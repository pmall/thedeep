namespace TheDeep;

/// <summary>
/// The numbers of a stacks backdrop (<see cref="StacksBackdrop"/>): sizes in meters per unit of the row's scale (a share is
/// of the row's pitch or of a span, as its name says), chances 0 to 1.
/// </summary>
public sealed record StacksBackdropSpec
{
    /// <summary>The approved look.</summary>
    public static readonly StacksBackdropSpec Default = new();

    /// <summary>The height of one tier of a tower; tiers stack up out of frame.</summary>
    public float TowerHeight { get; init; } = 6f;

    /// <summary>A tower's width along the row, as a share of the pitch; the walkways span the rest of the bay.</summary>
    public float TowerWidthShare { get; init; } = 0.5f;

    /// <summary>A walkway's thickness.</summary>
    public float WalkwayHeight { get; init; } = 0.5f;

    /// <summary>A walkway's depth toward the camera.</summary>
    public float WalkwayDepth { get; init; } = 2f;

    /// <summary>The chance a tower is missing.</summary>
    public float MissingTowerChance { get; init; } = 0.12f;

    /// <summary>A tower reaches the row's top less up to this share of the room's height (towers of uneven height).</summary>
    public float TowerShortfallShare { get; init; } = 0.45f;

    /// <summary>The chance a walkway between two tall enough towers is absent.</summary>
    public float NoWalkwayChance { get; init; } = 0.55f;

    /// <summary>How far icicles hang toward the camera under a walkway, as a share of its depth.</summary>
    public float IcicleDepthShare { get; init; } = 0.3f;

    /// <summary>A tower's depth toward the camera, times the row's scale.</summary>
    public float TowerDepth { get; init; } = 2.4f;

    /// <summary>A tower's shelf spacing, times the row's scale.</summary>
    public float TowerShelf { get; init; } = 1.2f;

    /// <summary>A walkway is this much longer than the bay between towers, times the row's scale.</summary>
    public float WalkwayOverhang { get; init; } = 0.4f;

    /// <summary>A walkway's bevel, times the row's scale.</summary>
    public float WalkwayBevel { get; init; } = 0.06f;

    /// <summary>An icicle fringe's width as a share of the bay between towers.</summary>
    public float IcicleWidthShare { get; init; } = 0.9f;

    /// <summary>An icicle fringe's length, times the row's scale.</summary>
    public float IcicleLength { get; init; } = 1.8f;

    /// <summary>The icicles in a fringe.</summary>
    public int IcicleCount { get; init; } = 9;
}
