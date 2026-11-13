using System.Numerics;

namespace TheDeep;

/// <summary>
/// What <see cref="BiomeRules"/> reads of one depth layer's look (its <c>BiomeProfile</c>): colors in sRGB, 0–1 a channel, as a
/// profile writes them; the light's colors named, so a problem says which.
/// </summary>
public sealed record BiomePalette
{
    public required Vector3 Rock { get; init; }
    public required Vector3 Metal { get; init; }
    public required Vector3 Rust { get; init; }

    /// <summary>The hazards' heat, coolest to hottest.</summary>
    public required Vector3 MoltenCool { get; init; }

    public required Vector3 Molten { get; init; }
    public required Vector3 MoltenHot { get; init; }

    /// <summary>The colors of the layer's light and murk (ambient, key, lamps, shafts, fog, haze, the void, particles, caustics), by name.</summary>
    public required (string Name, Vector3 Color)[] Light { get; init; }

    /// <summary>The values a profile states as shares, 0 to 1 (frost, masonry, shaft light, vignette), by name.</summary>
    public required (string Name, float Value)[] Shares { get; init; }
}
