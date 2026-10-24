using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>Paths for <see cref="MeshBuilder.Sweep"/>: lines, rounded polylines, sagging cables and arches.</summary>
public static class SweepPath
{
    /// <summary>
    /// Frames through <paramref name="points"/>. The up axis starts as close to <paramref name="upHint"/>
    /// as possible and is carried along without twisting (parallel transport).
    /// </summary>
    public static List<PathFrame> FromPoints(IReadOnlyList<Vector3> points, Vector3 upHint)
    {
        int count = points.Count;
        if (count < 2)
        {
            throw new ArgumentException("a path needs at least two points", nameof(points));
        }

        var frames = new List<PathFrame>(count);
        Vector3 up = Vector3.Zero;
        float distance = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 incoming = i > 0 ? SafeNormalize(points[i] - points[i - 1]) : Vector3.Zero;
            Vector3 outgoing = i < count - 1 ? SafeNormalize(points[i + 1] - points[i]) : Vector3.Zero;
            Vector3 tangent = SafeNormalize(incoming + outgoing);
            if (tangent == Vector3.Zero)
            {
                tangent = outgoing != Vector3.Zero ? outgoing : incoming;
            }

            if (i == 0)
            {
                up = Perpendicular(upHint, tangent);
            }
            else
            {
                distance += Vector3.Distance(points[i], points[i - 1]);
                up = Perpendicular(up, tangent);
            }

            Vector3 side = Vector3.Normalize(Vector3.Cross(up, tangent));
            frames.Add(new PathFrame(points[i], tangent, side, up, distance));
        }

        return frames;
    }

    /// <summary>A straight path from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static List<PathFrame> Line(Vector3 a, Vector3 b, Vector3 upHint) => FromPoints(new[] { a, b }, upHint);

    /// <summary>
    /// A polyline with each inner corner rounded over about <paramref name="bendRadius"/> meters
    /// (a pipe's bends), using <paramref name="bendSegments"/> segments per bend.
    /// </summary>
    public static List<PathFrame> Polyline(IReadOnlyList<Vector3> points, Vector3 upHint, float bendRadius, int bendSegments = 6)
    {
        var smoothed = new List<Vector3> { points[0] };
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 p = points[i];
            Vector3 before = points[i - 1] - p, after = points[i + 1] - p;
            float lenBefore = before.Length(), lenAfter = after.Length();
            Vector3 dirIn = -before / lenBefore, dirOut = after / lenAfter;
            float angle = MathF.Acos(Math.Clamp(Vector3.Dot(dirIn, dirOut), -1f, 1f));
            float trim = MathF.Min(bendRadius * MathF.Tan(angle * 0.5f), 0.5f * MathF.Min(lenBefore, lenAfter));
            if (angle < 1e-3f || trim < 1e-4f)
            {
                smoothed.Add(p);
                continue;
            }

            // A quadratic Bezier through the corner: close to a circular bend, tangent to both legs.
            Vector3 start = p - dirIn * trim, end = p + dirOut * trim;
            for (int s = 0; s <= bendSegments; s++)
            {
                float t = (float)s / bendSegments;
                float u = 1f - t;
                smoothed.Add(u * u * start + 2f * u * t * p + t * t * end);
            }
        }

        smoothed.Add(points[^1]);
        return FromPoints(smoothed, upHint);
    }

    /// <summary>A cable hanging from <paramref name="a"/> to <paramref name="b"/>, dipping <paramref name="sag"/> meters at its middle.</summary>
    public static List<PathFrame> Hanging(Vector3 a, Vector3 b, float sag, int segments)
    {
        segments = Math.Max(2, segments);
        var points = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            points[i] = Vector3.Lerp(a, b, t) - Vector3.UnitY * (sag * 4f * t * (1f - t));
        }

        return FromPoints(points, Vector3.UnitZ);
    }

    /// <summary>
    /// The center line of an arch in the XY plane, springing from (±span/2, 0) and rising to (0, rise),
    /// left to right. Up to a semicircle (rise ≤ span/2) it is one circular arc; higher, it is a pointed
    /// (Gothic) arch of two arcs meeting at the apex, with both halves mitred on the vertical plane there.
    /// The frames' side axis points out of the arch (radially), their up axis along +Z.
    /// </summary>
    public static List<List<PathFrame>> Arch(float span, float rise, int segments)
    {
        float half = span * 0.5f;
        segments = Math.Max(2, segments);
        if (rise <= half + 1e-4f)
        {
            // Circle through both springing points and the crown: center (0, c) below the springing line.
            float c = (rise * rise - half * half) / (2f * rise);
            float radius = rise - c;
            // 0f - c, not -c: a semicircle has c == 0, and atan2(-0, -x) is -π, which would sweep the wrong way.
            float start = MathF.Atan2(0f - c, -half), end = MathF.Atan2(0f - c, half);
            return new List<List<PathFrame>> { ArcFrames(new Vector2(0f, c), radius, start, end, segments, 0f) };
        }

        // Pointed: the left half is centered right of the middle at (cx, 0), and the right half mirrors it.
        float cx = (rise * rise - half * half) / span;
        float r = cx + half;
        float apexAngle = MathF.Atan2(rise, -cx);
        int halfSegments = Math.Max(2, segments / 2);
        List<PathFrame> left = ArcFrames(new Vector2(cx, 0f), r, MathF.PI, apexAngle, halfSegments, 0f);
        List<PathFrame> right = ArcFrames(new Vector2(-cx, 0f), r, MathF.PI - apexAngle, 0f, halfSegments, left[^1].Distance);

        // Mitre both halves on the plane x = 0: stretch the side offset so profile points land on it.
        Vector3 mitre = Vector3.UnitY * (r / rise);
        left[^1] = left[^1] with { SideOffset = mitre };
        right[0] = right[0] with { SideOffset = mitre };
        return new List<List<PathFrame>> { left, right };
    }

    /// <summary>Total length of a path.</summary>
    public static float Length(IReadOnlyList<PathFrame> frames) => frames[^1].Distance;

    /// <summary>The frame <paramref name="distance"/> meters along the path, interpolated between stations.</summary>
    public static PathFrame At(IReadOnlyList<PathFrame> frames, float distance)
    {
        distance = Math.Clamp(distance, 0f, frames[^1].Distance);
        for (int i = 1; i < frames.Count; i++)
        {
            PathFrame a = frames[i - 1], b = frames[i];
            if (distance <= b.Distance || i == frames.Count - 1)
            {
                float span = b.Distance - a.Distance;
                float t = span > 1e-6f ? (distance - a.Distance) / span : 0f;
                Vector3 tangent = SafeNormalize(Vector3.Lerp(a.Tangent, b.Tangent, t));
                Vector3 up = Perpendicular(Vector3.Lerp(a.Up, b.Up, t), tangent);
                Vector3 side = Vector3.Normalize(Vector3.Cross(up, tangent));
                return new PathFrame(Vector3.Lerp(a.Position, b.Position, t), tangent, side, up, distance);
            }
        }

        return frames[0];
    }

    /// <summary>
    /// The stretch of a path between two distances, as a straight two-station path along the local
    /// tangent there (flanges, collars). Its distances start at <paramref name="from"/>.
    /// </summary>
    public static List<PathFrame> Slice(IReadOnlyList<PathFrame> frames, float from, float to)
    {
        PathFrame mid = At(frames, (from + to) * 0.5f);
        float half = (to - from) * 0.5f;
        return new List<PathFrame>
        {
            mid with { Position = mid.Position - mid.Tangent * half, Distance = from },
            mid with { Position = mid.Position + mid.Tangent * half, Distance = to },
        };
    }

    private static List<PathFrame> ArcFrames(Vector2 center, float radius, float from, float to, int segments, float startDistance)
    {
        var frames = new List<PathFrame>(segments + 1);
        float sweep = to - from;
        float direction = MathF.Sign(sweep);
        for (int i = 0; i <= segments; i++)
        {
            float a = from + sweep * i / segments;
            var radial = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
            // Travelling with decreasing angle (clockwise) the tangent is (sin, -cos); increasing, its opposite.
            Vector3 tangent = new Vector3(MathF.Sin(a), -MathF.Cos(a), 0f) * -direction;
            Vector3 position = new Vector3(center, 0f) + radial * radius;
            Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, tangent));
            float distance = startDistance + MathF.Abs(sweep) * radius * i / segments;
            frames.Add(new PathFrame(position, tangent, side, Vector3.UnitZ, distance));
        }

        return frames;
    }

    /// <summary><paramref name="v"/> made perpendicular to unit <paramref name="axis"/> and normalized, or any perpendicular if parallel.</summary>
    private static Vector3 Perpendicular(Vector3 v, Vector3 axis)
    {
        Vector3 p = v - axis * Vector3.Dot(v, axis);
        if (p.LengthSquared() < 1e-8f)
        {
            Vector3 fallback = MathF.Abs(axis.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            p = fallback - axis * Vector3.Dot(fallback, axis);
        }

        return Vector3.Normalize(p);
    }

    private static Vector3 SafeNormalize(Vector3 v) => v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : Vector3.Zero;
}
