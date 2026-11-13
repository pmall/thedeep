using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// How the architecture behind a room sinks into its layer's murk (`docs/art-direction.md#midground`): each row deeper is
/// veiled more, in the color of the layer's far distance (its haze, what the rows stand against) kept darker than its rock,
/// so every row is darker and hazier than the play plane and a clear step behind the one in front, its edges fading into
/// what lies behind it, and the play plane's silhouette stays the strongest edge in the frame.
/// </summary>
public static class BackdropVeil
{
    /// <summary>The depth behind the play plane, meters, where the veil begins: a nearer row keeps its own value, the
    /// darkest of all, so the rows behind it step away from it.</summary>
    public const float ClearDepth = 5f;

    /// <summary>How many meters past <see cref="ClearDepth"/> the murk covers a row by 1 − 1/e (about 63%).</summary>
    public const float ClosingDepth = 22f;

    /// <summary>The most value the veil may have, as a share of the rock's (<see cref="BiomeRules.Value"/>): a fully veiled
    /// row stays darker than the play plane's rock.</summary>
    public const float UnderRock = 0.75f;

    /// <summary>How much of the murk covers a row <paramref name="depth"/> meters behind the play plane, 0–1.</summary>
    public static float Amount(float depth) => 1f - MathF.Exp(-MathF.Max(0f, depth - ClearDepth) / ClosingDepth);

    /// <summary>
    /// The color the rows sink into: the layer's <paramref name="haze"/>, dimmed where it would be brighter than
    /// <see cref="UnderRock"/> of its <paramref name="rock"/> (both sRGB, 0–1 a channel).
    /// </summary>
    public static Vector3 Color(Vector3 haze, Vector3 rock)
    {
        float ceiling = UnderRock * BiomeRules.Value(rock);
        float value = BiomeRules.Value(haze);
        return value <= ceiling ? haze : haze * (ceiling / value);
    }
}
