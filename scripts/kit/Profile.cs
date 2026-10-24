using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A closed cross-section swept along a path (<see cref="MeshBuilder.Sweep"/>): pipes, columns, arch rings.
/// X runs along the path frame's side axis, Y along its up axis. Each segment knows its outward normals
/// at both ends (equal on a flat side, turning on a rounded one) and where it sits on its patch, so
/// materials can find the profile's edges.
/// </summary>
public sealed class Profile
{
    /// <param name="VA">Position across the patch at <paramref name="A"/>, meters.</param>
    /// <param name="VB">Position across the patch at <paramref name="B"/>, meters.</param>
    /// <param name="PatchWidth">Width of the patch across the profile; negative when it wraps all the way round.</param>
    public readonly record struct Segment(Vector2 A, Vector2 B, Vector2 NormalA, Vector2 NormalB, float VA, float VB, float PatchWidth);

    public IReadOnlyList<Segment> Segments { get; }

    private Profile(IReadOnlyList<Segment> segments)
    {
        Segments = segments;
    }

    /// <summary>The outline points, in order (the start of every segment).</summary>
    public IEnumerable<Vector2> Outline()
    {
        foreach (Segment s in Segments)
        {
            yield return s.A;
        }
    }

    /// <summary>
    /// A circle (or a regular polygon). <paramref name="smooth"/>: one round patch that wraps around;
    /// otherwise each side is a flat patch with sharp corners (an octagonal column).
    /// </summary>
    public static Profile Circle(float radius, int sides, bool smooth = true)
    {
        sides = Math.Max(3, sides);
        var points = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = MathF.Tau * i / sides;
            points[i] = new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }

        if (!smooth)
        {
            return Polygon(points);
        }

        float circumference = MathF.Tau * radius;
        var segments = new Segment[sides];
        for (int i = 0; i < sides; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % sides];
            segments[i] = new Segment(a, b, Vector2.Normalize(a), Vector2.Normalize(b),
                circumference * i / sides, circumference * (i + 1) / sides, -circumference);
        }

        return new Profile(segments);
    }

    /// <summary>An ellipse with radii <paramref name="radii"/>, one round patch that wraps around (pod caps).</summary>
    public static Profile Ellipse(Vector2 radii, int sides)
    {
        sides = Math.Max(3, sides);
        var points = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = MathF.Tau * i / sides;
            points[i] = new Vector2(MathF.Cos(a) * radii.X, MathF.Sin(a) * radii.Y);
        }

        float length = 0f;
        var segments = new Segment[sides];
        for (int i = 0; i < sides; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % sides];
            float step = Vector2.Distance(a, b);
            segments[i] = new Segment(a, b, EllipseNormal(a, radii), EllipseNormal(b, radii), length, length + step, 0f);
            length += step;
        }

        for (int i = 0; i < sides; i++)
        {
            segments[i] = segments[i] with { PatchWidth = -length };
        }

        return new Profile(segments);
    }

    /// <summary>
    /// A rectangle centered on the path, <paramref name="width"/> along X and <paramref name="height"/> along Y,
    /// with its corners cut by a chamfer of <paramref name="bevel"/> meters whose normals turn smoothly from
    /// one side to the next (it reads as rounded). Each side is a patch; a chamfer belongs to the side before
    /// it and runs to that patch's edge.
    /// </summary>
    public static Profile Rect(float width, float height, float bevel)
    {
        float hx = width * 0.5f, hy = height * 0.5f;
        float r = Math.Clamp(bevel, 0f, 0.45f * MathF.Min(hx, hy));
        // Sides counterclockwise: bottom (normal -Y), right (+X), top (+Y), left (-X).
        Vector2[] normals = { -Vector2.UnitY, Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX };
        Vector2[] directions = { Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY };
        float[] lengths = { width, height, width, height };
        Vector2[] starts = { new(-hx, -hy), new(hx, -hy), new(hx, hy), new(-hx, hy) };

        var segments = new List<Segment>();
        for (int i = 0; i < 4; i++)
        {
            Vector2 n = normals[i], d = directions[i], next = normals[(i + 1) % 4];
            float len = lengths[i];
            Vector2 a = starts[i] + d * r;
            Vector2 b = starts[i] + d * (len - r);
            segments.Add(new Segment(a, b, n, n, r, len - r, len));
            if (r > 0f)
            {
                Vector2 c = starts[(i + 1) % 4] + directions[(i + 1) % 4] * r;
                segments.Add(new Segment(b, c, n, next, len - r, len, len));
            }
        }

        return new Profile(segments);
    }

    /// <summary>A polygon with sharp corners; each side is its own flat patch. Either winding is accepted.</summary>
    public static Profile Polygon(IReadOnlyList<Vector2> points)
    {
        int count = points.Count;
        if (count < 3)
        {
            throw new ArgumentException("a profile needs at least three points", nameof(points));
        }

        float area = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % count];
            area += a.X * b.Y - b.X * a.Y;
        }

        var ordered = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            ordered[i] = area >= 0f ? points[i] : points[count - 1 - i];
        }

        var segments = new Segment[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 a = ordered[i], b = ordered[(i + 1) % count];
            float len = Vector2.Distance(a, b);
            Vector2 d = (b - a) / MathF.Max(len, 1e-6f);
            // Counterclockwise outline: the outside is on the right of each side.
            var n = new Vector2(d.Y, -d.X);
            segments[i] = new Segment(a, b, n, n, 0f, len, len);
        }

        return new Profile(segments);
    }

    /// <summary>A gear seen face-on: <paramref name="teeth"/> square-ish teeth between two radii.</summary>
    public static Profile Gear(float innerRadius, float outerRadius, int teeth)
    {
        teeth = Math.Max(3, teeth);
        var points = new List<Vector2>();
        float step = MathF.Tau / teeth;
        for (int i = 0; i < teeth; i++)
        {
            float a = step * i;
            // Root, rise, crown, fall: crowns a bit narrower than the gaps.
            points.Add(Polar(innerRadius, a));
            points.Add(Polar(outerRadius, a + step * 0.12f));
            points.Add(Polar(outerRadius, a + step * 0.42f));
            points.Add(Polar(innerRadius, a + step * 0.54f));
        }

        return Polygon(points);
    }

    private static Vector2 EllipseNormal(Vector2 p, Vector2 radii)
    {
        var n = new Vector2(p.X / MathF.Max(radii.X * radii.X, 1e-8f), p.Y / MathF.Max(radii.Y * radii.Y, 1e-8f));
        return n.LengthSquared() > 1e-12f ? Vector2.Normalize(n) : Vector2.UnitX;
    }

    private static Vector2 Polar(float radius, float angle) => new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
}
