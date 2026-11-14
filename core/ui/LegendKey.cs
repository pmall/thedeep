namespace TheDeep;

/// <summary>
/// One entry of the map screen's legend (<see cref="MapSheet.Legend"/>): a kind of mark shown on the map, named once whatever the
/// number of its marks. The screen gives each its words and its color.
/// </summary>
public abstract record LegendKey
{
    private LegendKey()
    {
    }

    /// <summary>The ways the pilgrim can take now (<see cref="WayMark.Open"/>).</summary>
    public sealed record Open : LegendKey;

    /// <summary>The ways that need <paramref name="Ability"/>, which the pilgrim lacks.</summary>
    public sealed record Gate(Ability Ability) : LegendKey;

    /// <summary>The ways locked by an item of <paramref name="Kind"/> the pilgrim doesn't hold.</summary>
    public sealed record Lock(ItemKind Kind) : LegendKey;

    /// <summary>The ways leading where nothing is built (<see cref="WayMark.Unbuilt"/>).</summary>
    public sealed record Unbuilt : LegendKey;
}
