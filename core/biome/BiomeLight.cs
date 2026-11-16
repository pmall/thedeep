using System;
using System.Numerics;

namespace TheDeep;

/// <summary>The colour rules a biome's lights follow, on sRGB colors (0–1 a channel) as a profile writes them.</summary>
public static class BiomeLight
{
    /// <summary>The colour of a light shaft: <paramref name="color"/> scaled by <paramref name="share"/> in linear light, back in sRGB.</summary>
    public static Vector3 ShaftTint(Vector3 color, float share) => ToSrgb(ToLinear(color) * share);

    /// <summary>The <paramref name="color"/> taken <paramref name="amount"/> (0–1) of the way toward white, straight in sRGB.</summary>
    public static Vector3 Whiten(Vector3 color, float amount) => Vector3.Lerp(color, Vector3.One, amount);

    private static Vector3 ToLinear(Vector3 c) => new(ChannelToLinear(c.X), ChannelToLinear(c.Y), ChannelToLinear(c.Z));

    private static Vector3 ToSrgb(Vector3 c) => new(ChannelToSrgb(c.X), ChannelToSrgb(c.Y), ChannelToSrgb(c.Z));

    private static float ChannelToLinear(float c) => c < 0.04045f ? c * (1f / 12.92f) : MathF.Pow((c + 0.055f) * (1f / 1.055f), 2.4f);

    private static float ChannelToSrgb(float c) => c < 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;
}
