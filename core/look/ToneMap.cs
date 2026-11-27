using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A look's grade over the whole frame, on sRGB colors (0–1 a channel) as the frame leaves the tonemapper: each color's
/// value read on a ramp of three inks (<see cref="Shadow"/>, <see cref="Mid"/>, <see cref="Light"/>), so the world and the
/// figure come out printed in the same few inks, mixed in by <see cref="Amount"/>. Saturated colors keep <see cref="Keep"/>
/// of their own hue, so the accent and the hazards still mean gameplay (`docs/art-direction.md#ambiance`).
/// <see cref="Contrast"/> pushes values apart around the middle before the ramp. A smooth function of the color alone,
/// read through a lookup the engine applies to every frame alike: it decides nothing per pixel
/// (`docs/art-direction.md#stable-image`).
/// </summary>
public sealed record ToneMap(Vector3 Shadow, Vector3 Mid, Vector3 Light, float Amount, float Keep, float Contrast)
{
    /// <summary>Where on the value scale the middle ink sits.</summary>
    public const float MidValue = 0.45f;

    /// <summary>How saturated (the spread between a color's channels) a color must be to begin keeping its hue, and to keep
    /// all of <see cref="Keep"/>.</summary>
    private const float KeepFrom = 0.08f;

    private const float KeepFull = 0.35f;

    /// <summary>The graded color of <paramref name="color"/>.</summary>
    public Vector3 Apply(Vector3 color)
    {
        float value = Vector3.Dot(color, new Vector3(0.2126f, 0.7152f, 0.0722f));
        value = Math.Clamp(MidValue + (value - MidValue) * Contrast, 0f, 1f);
        Vector3 inked = value < MidValue
            ? Vector3.Lerp(Shadow, Mid, Smooth(value / MidValue))
            : Vector3.Lerp(Mid, Light, Smooth((value - MidValue) / (1f - MidValue)));
        float spread = MathF.Max(color.X, MathF.Max(color.Y, color.Z)) - MathF.Min(color.X, MathF.Min(color.Y, color.Z));
        float kept = Keep * Smooth(Math.Clamp((spread - KeepFrom) / (KeepFull - KeepFrom), 0f, 1f));
        // A kept color takes the ink's value with its own hue, so it sits in the print instead of glaring out of it.
        float own = MathF.Max(Vector3.Dot(color, new Vector3(0.2126f, 0.7152f, 0.0722f)), 1e-4f);
        float inkValue = Vector3.Dot(inked, new Vector3(0.2126f, 0.7152f, 0.0722f));
        Vector3 hued = Vector3.Clamp(color * MathF.Max(inkValue / own, 0.6f), Vector3.Zero, Vector3.One);
        Vector3 graded = Vector3.Lerp(inked, hued, kept);
        return Vector3.Lerp(color, graded, Amount);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
