using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The rules every depth layer's look keeps (`docs/art-direction.md#look`), read from its <see cref="BiomePalette"/>; the room check
/// holds every profile of a world to them. Metal reads apart from rock by value; rust stays muted; the hazards' heat runs from
/// dull to white-hot and is the most saturated color of the layer's light, so color means gameplay; shares stay within 0–1.
/// </summary>
public static class BiomeRules
{
    /// <summary>How much paler metal is than rock at least (<see cref="Value"/>): grippable metal reads at a glance.</summary>
    public const float MetalOverRock = 0.15f;

    /// <summary>The most chroma rust may have (<see cref="Chroma"/>): corrosion is decor, never a signal.</summary>
    public const float RustChroma = 0.25f;

    /// <summary>The most chroma any color of the layer's light may have, as a share of the molten color's: the hazard is what glows.</summary>
    public const float LightShareOfMolten = 0.6f;

    /// <summary>Every rule <paramref name="palette"/> breaks, one line each; none when it keeps them all.</summary>
    public static IEnumerable<string> Problems(BiomePalette palette)
    {
        float gap = Value(palette.Metal) - Value(palette.Rock);
        if (gap < MetalOverRock)
        {
            yield return $"metal is only {gap:0.00} paler than rock in value, under {MetalOverRock:0.00}: grip must read at a glance";
        }

        if (Chroma(palette.Rust) > RustChroma)
        {
            yield return $"rust's chroma {Chroma(palette.Rust):0.00} is over {RustChroma:0.00}: corrosion stays muted";
        }

        if (!(Value(palette.MoltenCool) < Value(palette.Molten) && Value(palette.Molten) < Value(palette.MoltenHot)))
        {
            yield return "the molten colors do not run from dull (MoltenCool) through Molten to white-hot (MoltenHot) in value";
        }

        float most = LightShareOfMolten * Chroma(palette.Molten);
        foreach ((string name, Vector3 color) in palette.Light)
        {
            if (Chroma(color) > most)
            {
                yield return $"{name}'s chroma {Chroma(color):0.00} is over {most:0.00} ({LightShareOfMolten:0.0} of the molten color's): only hazards glow saturated";
            }
        }

        foreach ((string name, float value) in palette.Shares)
        {
            if (value is < 0f or > 1f)
            {
                yield return $"{name} is {value}, outside 0–1";
            }
        }
    }

    /// <summary>A color's value as the eye weighs it (Rec. 709 weights on its sRGB channels), 0–1.</summary>
    public static float Value(Vector3 srgb) => 0.2126f * srgb.X + 0.7152f * srgb.Y + 0.0722f * srgb.Z;

    /// <summary>How far a color is from grey: its brightest channel less its dimmest, 0–1.</summary>
    public static float Chroma(Vector3 srgb) => MathF.Max(srgb.X, MathF.Max(srgb.Y, srgb.Z)) - MathF.Min(srgb.X, MathF.Min(srgb.Y, srgb.Z));
}
