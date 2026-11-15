namespace TheDeep;

/// <summary>The icicles a nave's bays and an archive's shelves share (<see cref="WallPiece.Icicles"/>): how many a fringe has.</summary>
public sealed record IcicleSpec
{
    /// <summary>The approved look.</summary>
    public static readonly IcicleSpec Default = new();

    /// <summary>A fringe has at least this many icicles...</summary>
    public int MinCount { get; init; } = 3;

    /// <summary>...and this many per meter of its width.</summary>
    public float PerMeter { get; init; } = 1.6f;
}
