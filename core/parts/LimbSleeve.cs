namespace TheDeep;

/// <summary>
/// Plated armor around a limb segment (<see cref="LimbShapes.Sleeve"/>): bands like a gauntlet's lames, each
/// flaring to its lower lip, the next one tucked under it, so the limb's outline steps. Sizes in meters;
/// widths are in the camera's plane (the silhouette), depths toward the camera.
/// </summary>
public sealed record LimbSleeve
{
    /// <summary>Where the plating starts along the limb, a share of its length.</summary>
    public float From { get; init; } = 0.1f;

    /// <summary>Where the plating ends along the limb, a share of its length.</summary>
    public float To { get; init; } = 0.9f;

    public int Bands { get; init; } = 3;

    public float WidthTop { get; init; } = 0.12f;
    public float WidthBottom { get; init; } = 0.1f;
    public float DepthTop { get; init; } = 0.1f;
    public float DepthBottom { get; init; } = 0.09f;

    /// <summary>How much wider each band's lower lip is than its top, a share of its width.</summary>
    public float Flare { get; init; } = 0.18f;

    /// <summary>The share of a band's length that the next band tucks under.</summary>
    public float Overlap { get; init; } = 0.15f;

    /// <summary>Moves the plating across the limb, in the camera's plane (positive is forward of a hanging limb).</summary>
    public float Shift { get; init; }

    public int Sides { get; init; } = 12;
}
