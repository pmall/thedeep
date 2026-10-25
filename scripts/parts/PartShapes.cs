using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The part library's shapes as pure geometry (Godot-free, unit tested), shared by the pilgrim and the
/// machines: smooth pods (limbs, heads, shells, bulbs), plates (armor, fins, blades) and rings (collars,
/// belts, visors). The part mesh resources (PodMesh, PlateMesh, RingMesh) expose them to assemblies.
/// A part's origin is where it attaches to its joint.
/// </summary>
public static class PartShapes
{
    /// <summary>
    /// A pod hanging from the origin along <paramref name="axis"/> for <paramref name="length"/> meters.
    /// Each station is (t from 0 to 1 along the length, width along X, depth along Z, shift along X), full
    /// sizes in meters; between stations the outline follows a smooth curve. Zero width and depth make a
    /// rounded pole. <paramref name="rings"/> stations are generated along the length.
    /// <paramref name="curl"/> bends the pod counterclockwise around Z (as the camera sees it) by that many
    /// radians over its length: a claw or a horn hooking forward, an antenna arching back. Width and shift
    /// then follow the bend.
    /// </summary>
    public static void Pod(MeshBuilder b, Vector3 axis, float length, IReadOnlyList<Vector4> stations, int sides, int rings, float curl = 0f)
    {
        if (stations.Count < 2)
        {
            throw new ArgumentException("a pod needs at least two stations", nameof(stations));
        }

        axis = axis.LengthSquared() > 1e-8f ? Vector3.Normalize(axis) : -Vector3.UnitY;
        rings = Math.Max(2, rings);
        var points = new Vector3[rings];
        var radii = new Vector2[rings];
        Vector3 along = Vector3.Zero;
        for (int i = 0; i < rings; i++)
        {
            float t = (float)i / (rings - 1);
            if (i > 0)
            {
                along += Arc(axis, curl, (float)(i - 1) / (rings - 1), t, length);
            }

            Vector4 s = Sample(stations, t);
            Vector3 direction = CurlAround(axis, curl * t);
            Vector3 shiftAxis = MathF.Abs(curl) > 1e-5f && MathF.Abs(direction.Z) < 0.99f
                ? Vector3.Normalize(new Vector3(-direction.Y, direction.X, 0f))
                : Vector3.UnitX;
            points[i] = along + shiftAxis * s.W;
            // Profile X runs along the frame's side (Z here), profile Y along its up (X, the silhouette).
            radii[i] = new Vector2(MathF.Max(0f, s.Z) * 0.5f, MathF.Max(0f, s.Y) * 0.5f);
        }

        b.Pod(SweepPath.FromPoints(points, UpHint(axis)), radii, sides);
    }

    /// <summary>
    /// A rotor spinning around Y at the origin: a hub <paramref name="hubRadius"/> wide and
    /// <paramref name="hubHeight"/> tall, and <paramref name="blades"/> blades out to <paramref name="radius"/>,
    /// <paramref name="chord"/> wide at the root and narrowing to the tip, pitched by <paramref name="pitch"/> radians.
    /// </summary>
    public static void Rotor(MeshBuilder b, float radius, int blades, float chord, float pitch, float hubRadius, float hubHeight)
    {
        b.Sweep(SweepPath.Line(new Vector3(0f, -hubHeight * 0.5f, 0f), new Vector3(0f, hubHeight * 0.5f, 0f), Vector3.UnitX),
            Profile.Circle(hubRadius, 16));
        blades = Math.Max(1, blades);
        float thickness = MathF.Max(0.006f, chord * 0.08f);
        var scales = new[] { Vector2.One, new Vector2(0.55f, 0.6f) };
        for (int k = 0; k < blades; k++)
        {
            float a = MathF.Tau * k / blades;
            var direction = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
            Vector3 trailing = Vector3.Cross(Vector3.UnitY, direction);
            Vector3 up = Vector3.UnitY * MathF.Cos(pitch) + trailing * MathF.Sin(pitch);
            b.Variation = (float)k / blades;
            b.Sweep(SweepPath.Line(direction * hubRadius * 0.8f, direction * radius, up),
                Profile.Rect(chord, thickness, thickness * 0.45f), scales: scales);
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// A track loop seen from the side, resting on the origin: <paramref name="length"/> between its wheel
    /// centers, <paramref name="radius"/> high to the wheels' center, <paramref name="width"/> deep along Z,
    /// the belt <paramref name="thickness"/> thick, with its road wheels inside. The belt is one patch that
    /// wraps along the loop (U runs around it), so a material can scroll cleats along it.
    /// </summary>
    public static void Tread(MeshBuilder b, float length, float radius, float width, float thickness, int wheels)
    {
        const int ArcSegments = 12;
        float r = MathF.Max(radius - thickness * 0.5f, 0.01f);
        float half = length * 0.5f;
        var points = new List<Vector3>();
        // Counterclockwise as the camera sees it: along the ground forward, up and around the front wheel,
        // back along the top, around the rear wheel.
        points.Add(new Vector3(-half, radius - r, 0f));
        for (int i = 0; i <= ArcSegments; i++)
        {
            float a = -MathF.PI * 0.5f + MathF.PI * i / ArcSegments;
            points.Add(new Vector3(half + MathF.Cos(a) * r, radius + MathF.Sin(a) * r, 0f));
        }

        for (int i = 0; i <= ArcSegments; i++)
        {
            float a = MathF.PI * 0.5f + MathF.PI * i / ArcSegments;
            points.Add(new Vector3(-half + MathF.Cos(a) * r, radius + MathF.Sin(a) * r, 0f));
        }

        points[^1] = points[0];
        List<PathFrame> path = SweepPath.FromPoints(points, Vector3.UnitZ);
        // The loop closes where it starts, on a straight run: both ends share the forward direction.
        path[0] = path[0] with { Tangent = Vector3.UnitX, Side = Vector3.UnitY, SideOffset = Vector3.UnitY, Up = Vector3.UnitZ };
        path[^1] = path[^1] with { Tangent = Vector3.UnitX, Side = Vector3.UnitY, SideOffset = Vector3.UnitY, Up = Vector3.UnitZ };
        b.Sweep(path, Profile.Rect(thickness, width, thickness * 0.3f), capStart: false, capEnd: false, wrapsAlong: true);

        wheels = Math.Max(2, wheels);
        float wheelRadius = MathF.Max(r - thickness * 0.6f, 0.01f);
        for (int i = 0; i < wheels; i++)
        {
            float x = -half + length * i / (wheels - 1);
            float wr = i == 0 || i == wheels - 1 ? wheelRadius : wheelRadius * 0.8f;
            float y = i == 0 || i == wheels - 1 ? radius : thickness + wr;
            b.Variation = 0.2f + 0.6f * i / (wheels - 1);
            b.Sweep(SweepPath.Line(new Vector3(x, y, -width * 0.42f), new Vector3(x, y, width * 0.42f), Vector3.UnitY),
                Profile.Circle(wr, 16));
            b.Sweep(SweepPath.Line(new Vector3(x, y, -width * 0.52f), new Vector3(x, y, width * 0.52f), Vector3.UnitY),
                Profile.Circle(wr * 0.35f, 10));
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// A drill bit pointing down -Y from the origin: a cone <paramref name="radius"/> wide at its collar,
    /// <paramref name="length"/> long, wound by <paramref name="flutes"/> helical ridges making
    /// <paramref name="turns"/> turns down to the tip.
    /// </summary>
    public static void Drill(MeshBuilder b, float length, float radius, int flutes, float turns)
    {
        float collar = length * 0.12f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, -collar, 0f), Vector3.UnitX), Profile.Circle(radius * 1.1f, 20));
        float cone = length - collar;
        Matrix4x4 placement = b.Transform;
        b.Transform = Matrix4x4.CreateTranslation(0f, -collar, 0f) * placement;
        Pod(b, -Vector3.UnitY, cone, new[] { new Vector4(0f, radius * 1.7f, radius * 1.7f, 0f), new Vector4(1f, 0f, 0f, 0f) }, 16, 8);
        b.Transform = placement;
        flutes = Math.Max(1, flutes);
        const int Steps = 40;
        for (int f = 0; f < flutes; f++)
        {
            var points = new Vector3[Steps + 1];
            var scales = new Vector2[Steps + 1];
            for (int i = 0; i <= Steps; i++)
            {
                float t = (float)i / Steps * 0.94f;
                float a = MathF.Tau * (turns * t + (float)f / flutes);
                float r = radius * 0.85f * (1f - t);
                points[i] = new Vector3(MathF.Cos(a) * r, -collar - t * cone, MathF.Sin(a) * r);
                scales[i] = new Vector2(1f - 0.85f * t, 1f - 0.85f * t);
            }

            b.Variation = (float)f / flutes;
            b.Sweep(SweepPath.FromPoints(points, Vector3.UnitY), Profile.Circle(radius * 0.22f, 8), scales: scales);
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// A plate hanging from the origin down -Y: <paramref name="width"/> along X, <paramref name="thickness"/>
    /// along Z, narrowing to <paramref name="taper"/> of its width at the far end, and bending toward +Z by
    /// <paramref name="curve"/> radians over its length (a shell, a knee guard, a fin).
    /// </summary>
    public static void Plate(MeshBuilder b, float width, float length, float thickness, float taper, float curve, float bevel)
    {
        const int Stations = 8;
        var points = new Vector3[Stations + 1];
        var scales = new Vector2[Stations + 1];
        for (int i = 0; i <= Stations; i++)
        {
            float t = (float)i / Stations;
            points[i] = Bend(t * length, curve, length);
            scales[i] = new Vector2(1f + (taper - 1f) * t, 1f);
        }

        Profile profile = Profile.Rect(width, thickness, MathF.Min(bevel, thickness * 0.45f));
        b.Sweep(SweepPath.FromPoints(points, Vector3.UnitZ), profile, scales: scales);
    }

    /// <summary>
    /// A band around the Y axis at the origin, on an ellipse of radii <paramref name="radiusX"/> (forward) and
    /// <paramref name="radiusZ"/> (side): <paramref name="height"/> tall, <paramref name="thickness"/> thick,
    /// covering <paramref name="arc"/> radians centered on +X (2π for a full collar or belt, less for a visor).
    /// </summary>
    public static void Ring(MeshBuilder b, float radiusX, float radiusZ, float height, float thickness, float arc, int segments)
    {
        arc = Math.Clamp(arc, 0.1f, MathF.Tau);
        bool closed = arc >= MathF.Tau - 1e-4f;
        segments = Math.Max(3, segments);
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float a = -arc * 0.5f + arc * i / segments;
            points[i] = new Vector3(MathF.Cos(a) * radiusX, 0f, MathF.Sin(a) * radiusZ);
        }

        if (closed)
        {
            points[segments] = points[0];
        }

        List<PathFrame> path = SweepPath.FromPoints(points, Vector3.UnitY);
        if (closed)
        {
            // Both ends of a closed ring meet: give them the same direction so the seam is invisible.
            PathFrame first = path[0], last = path[^1];
            Vector3 tangent = Vector3.Normalize(points[1] - points[segments - 1]);
            Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, tangent));
            path[0] = first with { Tangent = tangent, Side = side, SideOffset = side, Up = Vector3.UnitY };
            path[^1] = last with { Tangent = tangent, Side = side, SideOffset = side, Up = Vector3.UnitY };
        }

        Profile profile = Profile.Rect(thickness, height, MathF.Min(thickness, height) * 0.3f);
        b.Sweep(path, profile, capStart: !closed, capEnd: !closed, wrapsAlong: closed);
    }

    /// <summary>A station of <paramref name="stations"/> at <paramref name="t"/>, on a smooth curve through them (Catmull-Rom).</summary>
    public static Vector4 Sample(IReadOnlyList<Vector4> stations, float t)
    {
        int count = stations.Count;
        if (t <= stations[0].X)
        {
            return stations[0] with { X = t };
        }

        if (t >= stations[^1].X)
        {
            return stations[^1] with { X = t };
        }

        int i = 0;
        while (i < count - 2 && t > stations[i + 1].X)
        {
            i++;
        }

        Vector4 p0 = stations[Math.Max(i - 1, 0)], p1 = stations[i], p2 = stations[i + 1], p3 = stations[Math.Min(i + 2, count - 1)];
        float span = MathF.Max(p2.X - p1.X, 1e-6f);
        float u = (t - p1.X) / span;
        // Tangents scaled to this span, so uneven station spacing doesn't overshoot.
        Vector4 m1 = (p2 - p0) / MathF.Max(p2.X - p0.X, 1e-6f) * span;
        Vector4 m2 = (p3 - p1) / MathF.Max(p3.X - p1.X, 1e-6f) * span;
        float u2 = u * u, u3 = u2 * u;
        Vector4 value = (2f * u3 - 3f * u2 + 1f) * p1 + (u3 - 2f * u2 + u) * m1 + (-2f * u3 + 3f * u2) * p2 + (u3 - u2) * m2;
        return value with { X = t };
    }

    /// <summary>A point <paramref name="s"/> meters down a path that starts down -Y and bends toward +Z by <paramref name="curve"/> radians over <paramref name="length"/>.</summary>
    private static Vector3 Bend(float s, float curve, float length)
    {
        if (MathF.Abs(curve) < 1e-4f)
        {
            return new Vector3(0f, -s, 0f);
        }

        float radius = length / curve;
        float a = s / radius;
        return new Vector3(0f, -MathF.Sin(a) * radius, (1f - MathF.Cos(a)) * radius);
    }

    /// <summary><paramref name="axis"/> turned counterclockwise around Z by <paramref name="angle"/> radians.</summary>
    private static Vector3 CurlAround(Vector3 axis, float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new Vector3(c * axis.X - s * axis.Y, s * axis.X + c * axis.Y, axis.Z);
    }

    /// <summary>The way travelled from <paramref name="t0"/> to <paramref name="t1"/> (shares of the length) along a curling axis.</summary>
    private static Vector3 Arc(Vector3 axis, float curl, float t0, float t1, float length)
    {
        const int Steps = 8;
        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < Steps; i++)
        {
            float t = t0 + (t1 - t0) * (i + 0.5f) / Steps;
            sum += CurlAround(axis, curl * t);
        }

        return sum * ((t1 - t0) * length / Steps);
    }

    /// <summary>Keeps the profile's up axis on X (the silhouette the camera sees), unless the axis runs along X.</summary>
    private static Vector3 UpHint(Vector3 axis) => MathF.Abs(axis.X) > 0.9f ? Vector3.UnitY : Vector3.UnitX;
}
