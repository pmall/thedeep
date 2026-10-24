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
    /// </summary>
    public static void Pod(MeshBuilder b, Vector3 axis, float length, IReadOnlyList<Vector4> stations, int sides, int rings)
    {
        if (stations.Count < 2)
        {
            throw new ArgumentException("a pod needs at least two stations", nameof(stations));
        }

        axis = axis.LengthSquared() > 1e-8f ? Vector3.Normalize(axis) : -Vector3.UnitY;
        rings = Math.Max(2, rings);
        var points = new Vector3[rings];
        var radii = new Vector2[rings];
        for (int i = 0; i < rings; i++)
        {
            float t = (float)i / (rings - 1);
            Vector4 s = Sample(stations, t);
            points[i] = axis * (t * length) + Vector3.UnitX * s.W;
            // Profile X runs along the frame's side (Z here), profile Y along its up (X, the silhouette).
            radii[i] = new Vector2(MathF.Max(0f, s.Z) * 0.5f, MathF.Max(0f, s.Y) * 0.5f);
        }

        b.Pod(SweepPath.FromPoints(points, UpHint(axis)), radii, sides);
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

    /// <summary>Keeps the profile's up axis on X (the silhouette the camera sees), unless the axis runs along X.</summary>
    private static Vector3 UpHint(Vector3 axis) => MathF.Abs(axis.X) > 0.9f ? Vector3.UnitY : Vector3.UnitX;
}
