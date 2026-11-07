using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The part library's limbs (Godot-free, unit tested), shared by every species with arms or legs: a limb
/// segment's frame (<see cref="LimbFrame"/>) and the plated sleeve over it (<see cref="LimbSleeve"/>). Both
/// hang from the origin (the joint) along an axis, as <see cref="PartShapes.Pod"/> does, so a segment's frame
/// and sleeve are two parts on one joint, each in its own material: dark frame, plating at mid value
/// (docs/art-direction.md#machine-palette).
/// </summary>
public static class LimbShapes
{
    private const int HousingSides = 16;
    private const int RodSides = 10;

    /// <summary>A limb segment's frame along <paramref name="axis"/> for <paramref name="length"/> meters.</summary>
    public static void Frame(MeshBuilder b, Vector3 axis, float length, LimbFrame spec)
    {
        Vector3 dir = Direction(axis);
        Vector3 across = Across(dir);
        float w = MathF.Max(spec.Strut, 0.002f);
        float end = w * spec.StrutTaper;
        PartShapes.Pod(b, dir, length, new[] { new Vector4(0f, w, w, 0f), new Vector4(1f, end, end, 0f) }, RodSides, 3);

        if (spec.Knuckle > 0f)
        {
            Housing(b, Vector3.Zero, spec.Knuckle, spec.KnuckleDepth);
        }

        if (spec.EndKnuckle > 0f)
        {
            Housing(b, dir * length, spec.EndKnuckle, spec.KnuckleDepth * 0.8f);
        }

        if (spec.Piston > 0f)
        {
            Piston(b, dir, across, length, spec.Piston, spec.PistonSide);
        }

        for (int c = 0; c < spec.Cables; c++)
        {
            b.Variation = 0.3f + 0.5f * c / Math.Max(1, spec.Cables);
            float z = (c - (spec.Cables - 1) * 0.5f) * spec.CableWidth * 1.7f;
            float bow = spec.CableSide * (1f + 0.3f * c);
            const int Steps = 10;
            var points = new Vector3[Steps + 1];
            for (int i = 0; i <= Steps; i++)
            {
                float u = (float)i / Steps;
                float t = 0.1f + 0.78f * u;
                points[i] = dir * (length * t) + across * (bow * (0.3f + 0.7f * MathF.Sin(MathF.PI * u))) + new Vector3(0f, 0f, z);
            }

            b.Sweep(SweepPath.FromPoints(points, Vector3.UnitZ), Profile.Circle(spec.CableWidth * 0.5f, 6));
        }

        b.Variation = 0f;
    }

    /// <summary>A limb segment's plated sleeve along <paramref name="axis"/> for <paramref name="length"/> meters.</summary>
    public static void Sleeve(MeshBuilder b, Vector3 axis, float length, LimbSleeve spec)
    {
        Vector3 dir = Direction(axis);
        Vector3 across = Across(dir);
        int bands = Math.Max(1, spec.Bands);
        float overlap = Math.Clamp(spec.Overlap, -0.5f, 0.8f);
        float span = MathF.Max(spec.To - spec.From, 0.01f);
        // Bands of equal length fill the span, each overlapping the next by its share.
        float band = span / (bands - (bands - 1) * overlap);
        Matrix4x4 placement = b.Transform;
        for (int k = 0; k < bands; k++)
        {
            float t0 = spec.From + k * band * (1f - overlap), t1 = t0 + band;
            float wTop = Lerp(spec.WidthTop, spec.WidthBottom, (t0 - spec.From) / span);
            float wLip = Lerp(spec.WidthTop, spec.WidthBottom, (t1 - spec.From) / span);
            float dTop = Lerp(spec.DepthTop, spec.DepthBottom, (t0 - spec.From) / span);
            float dLip = Lerp(spec.DepthTop, spec.DepthBottom, (t1 - spec.From) / span);
            float grow = 1f + spec.Flare * 0.5f, shrink = 1f - spec.Flare * 0.5f;
            var stations = new[]
            {
                new Vector4(0f, wTop * shrink, dTop * shrink, 0f),
                new Vector4(0.75f, wLip * grow, dLip * grow, 0f),
                new Vector4(1f, wLip * grow * 0.97f, dLip * grow * 0.97f, 0f),
            };
            b.Variation = (float)k / bands;
            b.Transform = Matrix4x4.CreateTranslation(dir * (t0 * length) + across * spec.Shift) * placement;
            PartShapes.Pod(b, dir, band * length, stations, spec.Sides, 6);
        }

        b.Transform = placement;
        b.Variation = 0f;
    }

    /// <summary>A drum with its axle toward the camera, centered on <paramref name="center"/>, and the axle's boss on both faces.</summary>
    private static void Housing(MeshBuilder b, Vector3 center, float radius, float depth)
    {
        Vector3 half = new(0f, 0f, depth * 0.5f);
        b.Sweep(SweepPath.Line(center - half, center + half, Vector3.UnitX), Profile.Circle(radius, HousingSides));
        Vector3 boss = half + new Vector3(0f, 0f, radius * 0.3f);
        b.Sweep(SweepPath.Line(center - boss, center + boss, Vector3.UnitX), Profile.Circle(radius * 0.45f, RodSides));
    }

    /// <summary>A barrel and its rod along the strut, <paramref name="side"/> across it, held by a bracket at each end.</summary>
    private static void Piston(MeshBuilder b, Vector3 dir, Vector3 across, float length, float radius, float side)
    {
        Vector3 offset = across * side;
        Vector3 top = dir * (length * 0.1f), bottom = dir * (length * 0.88f), seam = dir * (length * 0.55f);
        b.Variation = 0.15f;
        b.Sweep(SweepPath.Line(top + offset, seam + offset, Vector3.UnitZ), Profile.Circle(radius, RodSides));
        b.Sweep(SweepPath.Line(seam - dir * (length * 0.04f) + offset, bottom + offset, Vector3.UnitZ), Profile.Circle(radius * 0.45f, RodSides));
        // The barrel's gland where the rod leaves it.
        b.Sweep(SweepPath.Line(seam - dir * (radius * 0.6f) + offset, seam + dir * (radius * 0.6f) + offset, Vector3.UnitZ),
            Profile.Circle(radius * 1.25f, RodSides));
        foreach (Vector3 mount in new[] { top, bottom })
        {
            b.Sweep(SweepPath.Line(mount, mount + offset, Vector3.UnitZ), Profile.Circle(radius * 0.4f, 8));
            Vector3 eye = new(0f, 0f, radius * 1.1f);
            b.Sweep(SweepPath.Line(mount + offset - eye, mount + offset + eye, Vector3.UnitX), Profile.Circle(radius * 0.8f, RodSides));
        }

        b.Variation = 0f;
    }

    private static Vector3 Direction(Vector3 axis) => axis.LengthSquared() > 1e-8f ? Vector3.Normalize(axis) : -Vector3.UnitY;

    /// <summary>At right angles to the limb in the camera's plane: forward (+X) for a limb hanging down.</summary>
    private static Vector3 Across(Vector3 dir)
    {
        var across = new Vector3(-dir.Y, dir.X, 0f);
        return across.LengthSquared() > 1e-6f ? Vector3.Normalize(across) : Vector3.UnitX;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
}
