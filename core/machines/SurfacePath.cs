using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The way a crawler moves over a surface: a path along the faces of a box (a metal block, as the camera
/// sees it), turning around its outer corners, at <c>standoff</c> meters from the faces. Faces that touch
/// something else (a block standing on the floor) are left out; the path is then open and ends at the
/// corners next to them. Position along the path is <c>s</c>, meters, counterclockwise (bottom, right,
/// top, left). Pure, so crawlers can be tested without the engine.
/// </summary>
public sealed class SurfacePath
{
    private const int CornerSegments = 6;

    private readonly List<Station> _stations;

    private SurfacePath(List<Station> stations, bool closed)
    {
        _stations = stations;
        Closed = closed;
        Length = stations[^1].S;
    }

    public float Length { get; }

    /// <summary>A loop around the whole box: <c>s</c> wraps instead of stopping at the ends.</summary>
    public bool Closed { get; }

    /// <summary>
    /// The path around the box from <paramref name="min"/> to <paramref name="max"/>. <paramref name="free"/>
    /// says which faces may be crawled on, in the order bottom, right, top, left. With faces left out, the
    /// longest run of free faces is kept. Null if no face is free.
    /// </summary>
    public static SurfacePath? AroundBox(Vector2 min, Vector2 max, IReadOnlyList<bool> free, float standoff)
    {
        // Each face: its start and end corner (counterclockwise) and outward normal.
        var corners = new[] { new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), new Vector2(max.X, max.Y), new Vector2(min.X, max.Y) };
        var normals = new[] { -Vector2.UnitY, Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX };

        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            count += free[i] ? 1 : 0;
        }

        if (count == 0)
        {
            return null;
        }

        bool closed = count == 4;
        int first = 0, run = 4;
        if (!closed)
        {
            // The longest run of free faces, starting right after a blocked one.
            run = 0;
            for (int start = 0; start < 4; start++)
            {
                if (!free[start] || free[(start + 3) % 4])
                {
                    continue;
                }

                int length = 0;
                while (length < 4 && free[(start + length) % 4])
                {
                    length++;
                }

                if (length > run)
                {
                    run = length;
                    first = start;
                }
            }
        }

        var stations = new List<Station>();
        void Add(Vector2 point, Vector2 normal)
        {
            float s = stations.Count == 0 ? 0f : stations[^1].S + Vector2.Distance(stations[^1].Point, point);
            stations.Add(new Station(point, normal, s));
        }

        for (int k = 0; k < run; k++)
        {
            int face = (first + k) % 4;
            Vector2 n = normals[face];
            Add(corners[face] + n * standoff, n);
            Add(corners[(face + 1) % 4] + n * standoff, n);
            if (k < run - 1 || closed)
            {
                // Around the outer corner to the next face.
                Vector2 corner = corners[(face + 1) % 4];
                Vector2 next = normals[(face + 1) % 4];
                for (int j = 1; j < CornerSegments; j++)
                {
                    float a = MathF.PI * 0.5f * j / CornerSegments;
                    Vector2 d = n * MathF.Cos(a) + next * MathF.Sin(a);
                    Add(corner + d * standoff, d);
                }
            }
        }

        if (closed)
        {
            Add(stations[0].Point, stations[0].Normal);
        }

        return new SurfacePath(stations, closed);
    }

    /// <summary>The point at <paramref name="s"/> and the surface's outward normal there.</summary>
    public (Vector2 Point, Vector2 Normal) At(float s)
    {
        s = Wrap(s);
        int i = 1;
        while (i < _stations.Count - 1 && _stations[i].S < s)
        {
            i++;
        }

        Station a = _stations[i - 1], b = _stations[i];
        float t = b.S > a.S ? Math.Clamp((s - a.S) / (b.S - a.S), 0f, 1f) : 0f;
        Vector2 normal = Vector2.Lerp(a.Normal, b.Normal, t);
        return (Vector2.Lerp(a.Point, b.Point, t), Vector2.Normalize(normal));
    }

    /// <summary>The direction of growing <c>s</c> at <paramref name="s"/>: the normal turned counterclockwise.</summary>
    public Vector2 Tangent(float s)
    {
        Vector2 n = At(s).Normal;
        return new Vector2(-n.Y, n.X);
    }

    /// <summary><paramref name="s"/> kept on the path: wrapped on a loop, clamped to the ends otherwise.</summary>
    public float Wrap(float s)
    {
        if (Closed)
        {
            return s - MathF.Floor(s / Length) * Length;
        }

        return Math.Clamp(s, 0f, Length);
    }

    /// <summary>Whether <paramref name="s"/> sits at an end of an open path.</summary>
    public bool AtEnd(float s, int direction) => !Closed && (direction > 0 ? s >= Length - 1e-3f : direction < 0 && s <= 1e-3f);

    /// <summary>The <c>s</c> of the point on the path closest to <paramref name="point"/>.</summary>
    public float Closest(Vector2 point)
    {
        float best = float.PositiveInfinity, bestS = 0f;
        for (int i = 1; i < _stations.Count; i++)
        {
            Station a = _stations[i - 1], b = _stations[i];
            Vector2 ab = b.Point - a.Point;
            float t = ab.LengthSquared() > 1e-12f ? Math.Clamp(Vector2.Dot(point - a.Point, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
            float d = Vector2.DistanceSquared(point, a.Point + ab * t);
            if (d < best)
            {
                best = d;
                bestS = a.S + (b.S - a.S) * t;
            }
        }

        return bestS;
    }

    private readonly record struct Station(Vector2 Point, Vector2 Normal, float S);
}
