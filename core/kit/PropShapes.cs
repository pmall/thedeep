using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The items' pieces as pure geometry (Godot-free, unit tested): a key, a power-up's crystal and the cradle an item floats over;
/// a door's lock is <see cref="LockShapes"/>, a lever <see cref="LeverShapes"/>. Each writes into a <see cref="MeshBuilder"/>; the
/// kit's prop meshes (KeyMesh, CrystalMesh, CradleMesh) expose them to scenes. A piece seen face-on faces the camera (+Z); X is
/// across the screen, Y up.
/// </summary>
public static class PropShapes
{
    /// <summary>
    /// A key standing along Y, its middle at the origin, its face toward the camera: a ring bow with a lozenge in it at the top,
    /// two collars under the bow, the shaft, and a bit of two wards at its foot, on +X. <paramref name="length"/> runs from the
    /// bow's top to the shaft's foot; <paramref name="thickness"/> is how thick the bow's ring and the bit are.
    /// </summary>
    public static void Key(MeshBuilder b, float length, float bowRadius, float thickness)
    {
        float top = length * 0.5f;
        float bowCenter = top - bowRadius;
        Matrix4x4 placement = b.Transform;

        // The bow: a ring in the XY plane, the profile's X radial, its Y along the depth.
        b.Variation = 0.2f;
        b.Sweep(SweepPath.Arc(new Vector2(0f, bowCenter), bowRadius - thickness * 0.5f, 0f, MathF.Tau, 28),
            Profile.Rect(thickness, thickness * 1.1f, thickness * 0.25f), capStart: false, capEnd: false, wrapsAlong: true);

        // A lozenge in the bow, a little thinner than the ring.
        float lozenge = (bowRadius - thickness) * 0.95f;
        b.Variation = 0.6f;
        b.Transform = Matrix4x4.CreateRotationZ(MathF.PI * 0.25f) * Matrix4x4.CreateTranslation(0f, bowCenter, 0f) * placement;
        b.Box(Vector3.Zero, new Vector3(lozenge, lozenge, thickness * 0.8f), thickness * 0.15f);
        b.Transform = placement;

        // Collars, then the shaft down to the foot.
        float shaft = thickness * 0.5f;
        float neck = bowCenter - bowRadius + thickness * 0.3f;
        b.Variation = 0.4f;
        float collar = thickness * 0.8f;
        b.Sweep(SweepPath.Line(new Vector3(0f, neck - collar, 0f), new Vector3(0f, neck, 0f), Vector3.UnitZ), Profile.Circle(shaft * 2.1f, 14));
        b.Sweep(SweepPath.Line(new Vector3(0f, neck - collar * 2.3f, 0f), new Vector3(0f, neck - collar * 1.6f, 0f), Vector3.UnitZ),
            Profile.Circle(shaft * 1.6f, 14));
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(new Vector3(0f, -top, 0f), new Vector3(0f, neck - collar * 0.5f, 0f), Vector3.UnitZ), Profile.Circle(shaft, 10));

        // The bit: a long ward at the foot and a shorter one above it, a notch between.
        float ward = bowRadius * 0.95f;
        float tall = length * 0.075f;
        b.Variation = 0.8f;
        b.Box(new Vector3(ward * 0.5f, -top + tall * 0.5f, 0f), new Vector3(ward, tall, thickness * 0.8f), thickness * 0.12f);
        b.Box(new Vector3(ward * 0.36f, -top + tall * 2.1f, 0f), new Vector3(ward * 0.72f, tall * 0.9f, thickness * 0.8f), thickness * 0.12f);
        b.Variation = 0f;
    }

    /// <summary>
    /// A crystal around Y: a band of <paramref name="sides"/> flat facets, <paramref name="band"/> tall and <paramref name="radius"/>
    /// wide at its corners, between two points, <paramref name="top"/> above the band and <paramref name="bottom"/> below it. Every
    /// facet is flat-shaded, so each catches the light on its own. Its middle (the band's) is at the origin.
    /// </summary>
    public static void Crystal(MeshBuilder b, float radius, int sides, float band, float top, float bottom)
    {
        sides = Math.Max(3, sides);
        float low = -band * 0.5f, high = band * 0.5f;
        var tipTop = new Vector3(0f, high + top, 0f);
        var tipBottom = new Vector3(0f, low - bottom, 0f);
        for (int k = 0; k < sides; k++)
        {
            float a0 = MathF.Tau * k / sides, a1 = MathF.Tau * (k + 1) / sides;
            var c0 = new Vector3(MathF.Cos(a0) * radius, 0f, MathF.Sin(a0) * radius);
            var c1 = new Vector3(MathF.Cos(a1) * radius, 0f, MathF.Sin(a1) * radius);
            b.Variation = (float)k / sides;
            if (band > 1e-4f)
            {
                Facet(b, c0 + Vector3.UnitY * low, c1 + Vector3.UnitY * low, c1 + Vector3.UnitY * high, c0 + Vector3.UnitY * high);
            }

            Facet(b, c0 + Vector3.UnitY * high, c1 + Vector3.UnitY * high, tipTop);
            Facet(b, c1 + Vector3.UnitY * low, c0 + Vector3.UnitY * low, tipBottom);
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// The cradle an item floats over, standing on the origin (a pedestal's top): <paramref name="prongs"/> curved prongs rising
    /// from a collar <paramref name="radius"/> wide, leaning out and turning back in at <paramref name="height"/>, like the claws of
    /// a reliquary. One prong points away from the camera, so the others frame the item from both sides.
    /// </summary>
    public static void Cradle(MeshBuilder b, float radius, float height, int prongs)
    {
        prongs = Math.Max(2, prongs);
        float bar = radius * 0.16f;
        b.Variation = 0f;
        Matrix4x4 placement = b.Transform;
        // The collar: a ring laid flat (the arc's plane turned from XY to XZ).
        b.Transform = Matrix4x4.CreateRotationX(-MathF.PI * 0.5f) * Matrix4x4.CreateTranslation(0f, bar * 0.8f, 0f) * placement;
        b.Sweep(SweepPath.Arc(Vector2.Zero, radius * 0.86f, 0f, MathF.Tau, 28), Profile.Rect(bar * 1.4f, bar * 1.6f, bar * 0.3f),
            capStart: false, capEnd: false, wrapsAlong: true);
        b.Transform = placement;
        const int Steps = 10;
        for (int p = 0; p < prongs; p++)
        {
            float a = -MathF.PI * 0.5f + MathF.Tau * p / prongs;
            var outward = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
            var points = new Vector3[Steps + 1];
            for (int i = 0; i <= Steps; i++)
            {
                float t = (float)i / Steps;
                // Out from the collar, then hooking back in over the item at the top, like a claw.
                float r = radius * (0.86f + 0.42f * MathF.Sin(MathF.PI * t * 0.75f) - 0.55f * t * t * t);
                points[i] = outward * r + Vector3.UnitY * (height * MathF.Sin(MathF.PI * 0.5f * t));
            }

            b.Variation = 0.3f + 0.6f * p / prongs;
            b.Sweep(SweepPath.FromPoints(points, outward), Profile.Rect(bar, bar * 1.4f, bar * 0.25f),
                scales: Taper(Steps + 1, 0.4f));
        }

        b.Variation = 0f;
    }

    private static Vector2[] Taper(int stations, float end)
    {
        var scales = new Vector2[stations];
        for (int i = 0; i < stations; i++)
        {
            float s = 1f + (end - 1f) * i / (stations - 1);
            scales[i] = new Vector2(s, s);
        }

        return scales;
    }

    /// <summary>A flat facet through its corners (a triangle or a quad, in order round it), facing away from the crystal's middle.</summary>
    private static void Facet(MeshBuilder b, params Vector3[] corners)
    {
        Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
        Vector3 middle = Vector3.Zero;
        foreach (Vector3 c in corners)
        {
            middle += c;
        }

        middle /= corners.Length;
        if (Vector3.Dot(normal, middle) < 0f)
        {
            normal = -normal;
        }

        normal = Vector3.Normalize(normal);
        Vector3 u = Vector3.Normalize(corners[1] - corners[0]);
        Vector3 v = Vector3.Cross(normal, u);
        float width = Vector3.Distance(corners[0], corners[1]);
        float tall = MathF.Abs(Vector3.Dot(corners[2] - corners[0], v));
        var patch = new Vector2(width, MathF.Max(tall, 1e-3f));
        Span<int> ids = stackalloc int[4];
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 d = corners[i] - corners[0];
            ids[i] = b.Vertex(corners[i], normal, u, v, new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, v)), patch);
        }

        b.Triangle(ids[0], ids[1], ids[2]);
        if (corners.Length == 4)
        {
            b.Triangle(ids[0], ids[2], ids[3]);
        }
    }
}
