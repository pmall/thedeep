using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The interactables' pieces as pure geometry (Godot-free, unit tested): a key, a power-up's crystal, a door's lock (a key's boss
/// and keyhole, a code's seal, a mechanism's cog), a lever's housing and handle, the cradle an item floats over. Each writes into a
/// <see cref="MeshBuilder"/>; the kit's prop meshes (KeyMesh, CrystalMesh, LockMesh, LeverMesh, CradleMesh) expose them to scenes.
/// A piece seen face-on faces the camera (+Z); X is across the screen, Y up.
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
    /// A lock's boss facing the camera, its back on the origin's plane: a round housing <paramref name="radius"/> wide and
    /// <paramref name="depth"/> deep, a raised rim round its face, and <paramref name="studs"/> studs between rim and middle.
    /// </summary>
    public static void Boss(MeshBuilder b, float radius, float depth, int studs)
    {
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Circle(radius * 0.9f, 28), capStart: false);
        b.Variation = 0.3f;
        Matrix4x4 placement = b.Transform;
        b.Transform = Matrix4x4.CreateTranslation(0f, 0f, depth * 0.6f) * placement;
        b.Sweep(SweepPath.Arc(Vector2.Zero, radius * 0.93f, 0f, MathF.Tau, 32), Profile.Rect(radius * 0.14f, depth * 1.2f, radius * 0.04f),
            capStart: false, capEnd: false, wrapsAlong: true);
        b.Transform = placement;
        float stud = radius * 0.07f;
        for (int i = 0; i < studs; i++)
        {
            float a = MathF.Tau * (i + 0.5f) / studs;
            var at = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * (radius * 0.72f);
            b.Variation = 0.5f + 0.4f * i / Math.Max(1, studs);
            b.Sweep(SweepPath.Line(at + new Vector3(0f, 0f, depth * 0.9f), at + new Vector3(0f, 0f, depth + stud), Vector3.UnitY),
                Profile.Circle(stud, 8), capStart: false);
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// A keyhole <paramref name="height"/> tall facing the camera, <paramref name="depth"/> thick, its back on the origin's plane: a
    /// round eye over a slot that flares toward its foot. The eye's middle is at the origin.
    /// </summary>
    public static void Keyhole(MeshBuilder b, float height, float depth)
    {
        float r = height * 0.3f;
        float foot = -(height - r);
        var points = new List<Vector2>();
        const int Arc = 18;
        // From the slot's right shoulder, round the eye counterclockwise to its left shoulder, then down the slot.
        float from = -MathF.PI * 0.32f, to = MathF.PI * 1.32f;
        for (int i = 0; i <= Arc; i++)
        {
            float a = from + (to - from) * i / Arc;
            points.Add(new Vector2(MathF.Cos(a), MathF.Sin(a)) * r);
        }

        points.Add(new Vector2(-r * 0.62f, foot));
        points.Add(new Vector2(r * 0.62f, foot));
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Polygon(points));
    }

    /// <summary>
    /// A cog facing the camera, its back on the origin's plane: <paramref name="teeth"/> teeth out to <paramref name="radius"/>,
    /// <paramref name="depth"/> deep, a hub standing proud of it and spokes' windows between.
    /// </summary>
    public static void Cog(MeshBuilder b, float radius, int teeth, float depth)
    {
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Gear(radius * 0.84f, radius, teeth));
        b.Variation = 0.5f;
        b.Sweep(SweepPath.Line(new Vector3(0f, 0f, depth * 0.5f), new Vector3(0f, 0f, depth * 1.35f), Vector3.UnitY),
            Profile.Circle(radius * 0.36f, 18), capStart: false);
        b.Variation = 0.8f;
        Matrix4x4 placement = b.Transform;
        b.Transform = Matrix4x4.CreateTranslation(0f, 0f, depth * 0.58f) * placement;
        b.Sweep(SweepPath.Arc(Vector2.Zero, radius * 0.62f, 0f, MathF.Tau, 28), Profile.Rect(radius * 0.08f, depth * 1.15f, radius * 0.02f),
            capStart: false, capEnd: false, wrapsAlong: true);
        b.Transform = placement;
        b.Variation = 0f;
    }

    /// <summary>
    /// A code's seal facing the camera, its back on the origin's plane: a square plate <paramref name="size"/> wide with its corners
    /// cut, <paramref name="depth"/> deep, a raised frame round its face and a stud at each cut corner.
    /// </summary>
    public static void Seal(MeshBuilder b, float size, float depth)
    {
        float h = size * 0.5f, cut = size * 0.18f;
        var outline = new[]
        {
            new Vector2(-h + cut, -h), new Vector2(h - cut, -h), new Vector2(h, -h + cut), new Vector2(h, h - cut),
            new Vector2(h - cut, h), new Vector2(-h + cut, h), new Vector2(-h, h - cut), new Vector2(-h, -h + cut),
        };
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Polygon(outline), capStart: false);
        float bar = size * 0.08f;
        b.Variation = 0.3f;
        foreach (float s in new[] { -1f, 1f })
        {
            b.Box(new Vector3(0f, s * (h - bar * 0.5f - cut * 0.2f), depth + bar * 0.3f), new Vector3(size - 2f * cut, bar, bar * 0.9f), bar * 0.2f);
            b.Box(new Vector3(s * (h - bar * 0.5f - cut * 0.2f), 0f, depth + bar * 0.3f), new Vector3(bar, size - 2f * cut, bar * 0.9f), bar * 0.2f);
        }

        b.Variation = 0.6f;
        for (int i = 0; i < 4; i++)
        {
            float a = MathF.PI * 0.25f + MathF.PI * 0.5f * i;
            var at = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * ((h - cut * 0.4f) * MathF.Sqrt(2f) * 0.86f);
            b.Sweep(SweepPath.Line(at + new Vector3(0f, 0f, depth * 0.5f), at + new Vector3(0f, 0f, depth + bar * 0.6f), Vector3.UnitY),
                Profile.Circle(bar * 0.45f, 8), capStart: false);
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// A lever's housing standing on the origin, its handle's pivot <paramref name="pivotHeight"/> above the floor: a stepped plinth
    /// (<paramref name="size"/>: width, height, depth) with ribs down its front, two cheeks holding the pivot's drum between them,
    /// and a quadrant of two rails arching over the pivot at <paramref name="guideRadius"/>, <paramref name="guideAngle"/> radians
    /// either side of upright, its ticks marking the throw, its ends stayed down to the plinth's lower step.
    /// </summary>
    public static void LeverBase(MeshBuilder b, Vector3 size, float pivotHeight, float guideRadius, float guideAngle)
    {
        float low = size.Y * 0.4f;
        b.Variation = 0f;
        b.Box(new Vector3(0f, low * 0.5f, 0f), new Vector3(size.X, low, size.Z), 0.03f);
        b.Variation = 0.15f;
        b.Box(new Vector3(0f, low + (size.Y - low) * 0.5f, 0f), new Vector3(size.X * 0.78f, size.Y - low, size.Z * 0.8f), 0.025f);

        // Ribs down the plinth's front.
        b.Variation = 0.35f;
        for (int i = -1; i <= 1; i += 2)
        {
            b.Box(new Vector3(i * size.X * 0.28f, size.Y * 0.5f, size.Z * 0.42f), new Vector3(size.X * 0.06f, size.Y * 0.9f, size.Z * 0.1f), 0.01f);
        }

        // The cheeks and the drum between them.
        float cheekZ = size.Z * 0.24f, cheek = size.Z * 0.08f;
        float cheekTop = pivotHeight + size.X * 0.12f;
        b.Variation = 0.5f;
        foreach (float s in new[] { -1f, 1f })
        {
            b.Box(new Vector3(0f, (size.Y + cheekTop) * 0.5f, s * cheekZ), new Vector3(size.X * 0.32f, cheekTop - size.Y, cheek), 0.015f);
        }

        b.Variation = 0.7f;
        b.Sweep(SweepPath.Line(new Vector3(0f, pivotHeight, -cheekZ - cheek), new Vector3(0f, pivotHeight, cheekZ + cheek), Vector3.UnitY),
            Profile.Circle(size.X * 0.1f, 18));

        // The quadrant: two rails over the pivot, ticks across them, stays down to the plinth.
        float from = MathF.PI * 0.5f - guideAngle, to = MathF.PI * 0.5f + guideAngle;
        var pivot = new Vector2(0f, pivotHeight);
        float rail = size.X * 0.05f;
        b.Variation = 0.25f;
        foreach (float s in new[] { -1f, 1f })
        {
            Matrix4x4 placement = b.Transform;
            b.Transform = Matrix4x4.CreateTranslation(0f, 0f, s * cheekZ) * placement;
            b.Sweep(SweepPath.Arc(pivot, guideRadius, from, to, 16), Profile.Rect(rail, rail * 0.8f, rail * 0.2f));
            b.Transform = placement;
        }

        b.Variation = 0.9f;
        const int Ticks = 5;
        for (int i = 0; i < Ticks; i++)
        {
            float a = from + (to - from) * i / (Ticks - 1);
            var radial = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
            Vector3 at = new Vector3(pivot, 0f) + radial * (guideRadius + rail * 1.2f);
            bool end = i == 0 || i == Ticks - 1;
            b.Sweep(SweepPath.Line(at - radial * rail * 1.4f, at + radial * rail * (end ? 1.6f : 0.9f), Vector3.UnitZ),
                Profile.Rect(rail * (end ? 1.1f : 0.6f), cheekZ * 2f + rail, rail * 0.15f));
        }

        b.Variation = 0.45f;
        foreach (float a in new[] { from, to })
        {
            var radial = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
            Vector3 end = new Vector3(pivot, 0f) + radial * guideRadius;
            var foot = new Vector3(MathF.Sign(end.X) * MathF.Min(MathF.Abs(end.X), size.X * 0.44f), low, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                var z = new Vector3(0f, 0f, s * cheekZ);
                b.Sweep(SweepPath.Line(foot + z, end + z, Vector3.UnitZ), Profile.Rect(rail * 0.8f, rail * 0.8f, rail * 0.2f));
            }
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// A lever's handle rising from its pivot at the origin along +Y, <paramref name="length"/> long and <paramref name="radius"/>
    /// thick: a hub across the pivot, a tapering shaft with a collar, and the socket the grip sits across at its top.
    /// </summary>
    public static void LeverHandle(MeshBuilder b, float length, float radius)
    {
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(new Vector3(0f, 0f, -radius * 1.6f), new Vector3(0f, 0f, radius * 1.6f), Vector3.UnitY),
            Profile.Circle(radius * 2.4f, 16));
        b.Variation = 0.3f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, length, 0f), Vector3.UnitZ), Profile.Circle(radius, 12),
            scales: new[] { Vector2.One, new Vector2(0.75f, 0.75f) });
        b.Variation = 0.6f;
        b.Sweep(SweepPath.Line(new Vector3(0f, length * 0.3f, 0f), new Vector3(0f, length * 0.3f + radius * 2f, 0f), Vector3.UnitZ),
            Profile.Circle(radius * 1.7f, 12));
        b.Sweep(SweepPath.Line(new Vector3(0f, length - radius * 2.2f, 0f), new Vector3(0f, length, 0f), Vector3.UnitZ),
            Profile.Circle(radius * 1.5f, 12));
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
