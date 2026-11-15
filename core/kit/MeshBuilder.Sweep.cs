using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>The sweep, pod and cap primitives of <see cref="MeshBuilder"/>: shapes that run along a path.</summary>
public sealed partial class MeshBuilder
{
    /// <summary>
    /// Sweeps <paramref name="profile"/> along <paramref name="path"/>. U runs along the path (from its
    /// first station), V across each profile side. Ends are closed with flat caps when asked.
    /// <paramref name="scales"/>, one per station, stretches the profile there along its X and Y (a tapering
    /// blade or coat tail); normals follow the stretch, not the taper along the path. <paramref name="wrapsAlong"/>
    /// marks a path that closes on itself (a ring): its patches have no edge where it starts and ends.
    /// </summary>
    public void Sweep(IReadOnlyList<PathFrame> path, Profile profile, bool capStart = true, bool capEnd = true,
        IReadOnlyList<Vector2>? scales = null, bool wrapsAlong = false)
    {
        float start = path[0].Distance;
        float length = path[^1].Distance - start;
        for (int si = 0; si < profile.Segments.Count; si++) // not foreach: over the interface it boxes an enumerator
        {
            Profile.Segment s = profile.Segments[si];
            int previousA = -1, previousB = -1;
            Vector2 run = s.B - s.A;
            float runLength = run.Length();
            Vector2 runDirection = runLength > 1e-6f ? run / runLength : Vector2.UnitX;
            for (int i = 0; i < path.Count; i++)
            {
                PathFrame f = path[i];
                Vector2 k = scales?[i] ?? Vector2.One;
                // V is measured along the side, so it stretches with the profile axis the side runs along.
                float vScale = (runDirection * k).Length();
                Vector3 pa = f.Place(s.A * k), pb = f.Place(s.B * k);
                Vector3 across = pb - pa;
                var size = new Vector2(wrapsAlong ? -length : length, s.PatchWidth * vScale);
                float u = f.Distance - start;
                int ia = Vertex(pa, f.Direction(ScaledNormal(s.NormalA, k)), f.Tangent, across, new Vector2(u, s.VA * vScale), size);
                int ib = Vertex(pb, f.Direction(ScaledNormal(s.NormalB, k)), f.Tangent, across, new Vector2(u, s.VB * vScale), size);
                if (previousA >= 0)
                {
                    Quad(previousA, previousB, ib, ia);
                }

                previousA = ia;
                previousB = ib;
            }
        }

        if (capStart)
        {
            Cap(path[0], profile, -1f, scales?[0] ?? Vector2.One);
        }

        if (capEnd)
        {
            Cap(path[^1], profile, 1f, scales?[^1] ?? Vector2.One);
        }
    }

    /// <summary>A profile normal after the profile is stretched by <paramref name="scale"/> (normals scale inversely).</summary>
    private static Vector2 ScaledNormal(Vector2 normal, Vector2 scale)
    {
        var n = new Vector2(normal.X / MathF.Max(scale.X, 1e-4f), normal.Y / MathF.Max(scale.Y, 1e-4f));
        return n.LengthSquared() > 1e-12f ? Vector2.Normalize(n) : normal;
    }

    /// <summary>
    /// A smooth closed body around <paramref name="path"/> (limbs, heads, shells, bulbs): at each station an
    /// ellipse with radii <paramref name="radii"/> (X along the frame's side axis, Y along its up axis),
    /// <paramref name="sides"/> points around. A station with zero radii is a pole, which rounds the end off;
    /// an end with a radius is closed by a flat cap. Normals come from the surface itself, so tapers, bulges
    /// and poles shade smoothly. U runs along the path, V around the girth (a patch that wraps).
    /// </summary>
    public void Pod(IReadOnlyList<PathFrame> path, IReadOnlyList<Vector2> radii, int sides)
    {
        int rings = path.Count;
        sides = Math.Max(3, sides);
        int columns = sides + 1; // the seam column repeats the first, so V can run to the full girth
        var grid = new Vector3[rings, columns];
        var girthAxes = new Vector3[rings, columns];
        float maxGirth = 0f;
        for (int j = 0; j < rings; j++)
        {
            PathFrame f = path[j];
            Vector2 r = radii[j];
            maxGirth = MathF.Max(maxGirth, Girth(r));
            for (int k = 0; k < columns; k++)
            {
                float a = MathF.Tau * k / sides;
                float cos = MathF.Cos(a), sin = MathF.Sin(a);
                grid[j, k] = f.Position + f.Side * (cos * r.X) + f.Up * (sin * r.Y);
                // Direction of growing V: the ellipse's tangent (a circle's at a pole).
                Vector3 along = f.Side * (-sin * MathF.Max(r.X, 1e-3f)) + f.Up * (cos * MathF.Max(r.Y, 1e-3f));
                girthAxes[j, k] = Vector3.Normalize(along);
            }
        }

        float start = path[0].Distance;
        var size = new Vector2(path[^1].Distance - start, -MathF.Max(maxGirth, 1e-3f));
        var index = new int[rings, columns];
        for (int j = 0; j < rings; j++)
        {
            PathFrame f = path[j];
            float girth = Girth(radii[j]);
            for (int k = 0; k < columns; k++)
            {
                Vector3 alongPath = grid[Math.Min(j + 1, rings - 1), k] - grid[Math.Max(j - 1, 0), k];
                Vector3 alongGirth = grid[j, (k + 1) % sides] - grid[j, (k + sides - 1) % sides];
                Vector3 normal = Vector3.Cross(alongGirth, alongPath);
                Vector3 outward = grid[j, k] - f.Position;
                if (normal.LengthSquared() < 1e-12f || outward.LengthSquared() < 1e-10f)
                {
                    // A pole: the surface faces along the path.
                    normal = j == 0 ? -f.Tangent : j == rings - 1 ? f.Tangent : outward;
                }
                else if (Vector3.Dot(normal, outward) < 0f)
                {
                    normal = -normal;
                }

                var uv = new Vector2(f.Distance - start, girth * k / sides);
                index[j, k] = Vertex(grid[j, k], normal, f.Tangent, girthAxes[j, k], uv, size);
            }
        }

        for (int j = 0; j + 1 < rings; j++)
        {
            for (int k = 0; k < sides; k++)
            {
                Quad(index[j, k], index[j, k + 1], index[j + 1, k + 1], index[j + 1, k]);
            }
        }

        if (radii[0].LengthSquared() > 1e-10f)
        {
            Cap(path[0], Profile.Ellipse(radii[0], sides), -1f, Vector2.One);
        }

        if (radii[^1].LengthSquared() > 1e-10f)
        {
            Cap(path[^1], Profile.Ellipse(radii[^1], sides), 1f, Vector2.One);
        }
    }

    /// <summary>Approximate circumference of an ellipse with radii <paramref name="r"/> (Ramanujan).</summary>
    private static float Girth(Vector2 r)
    {
        float a = MathF.Abs(r.X), b = MathF.Abs(r.Y);
        return MathF.PI * (3f * (a + b) - MathF.Sqrt((3f * a + b) * (a + 3f * b)));
    }

    /// <summary>
    /// A flat cap closing <paramref name="profile"/> at <paramref name="frame"/>, facing along the path
    /// (<paramref name="facing"/> +1) or against it (-1). A fan from the outline's center, so the profile
    /// must be star-shaped around it (circles, rectangles, gears are).
    /// </summary>
    public void Cap(PathFrame frame, Profile profile, float facing) => Cap(frame, profile, facing, Vector2.One);

    private void Cap(PathFrame frame, Profile profile, float facing, Vector2 scale)
    {
        // Allocation-free: chains are rebuilt every frame (PartBody).
        IReadOnlyList<Vector2> outline = profile.Outline();
        Vector2 min = outline[0] * scale, max = min;
        Vector2 sum = Vector2.Zero;
        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 p = outline[i] * scale;
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
            sum += p;
        }

        Vector2 size = max - min;
        Vector3 normal = frame.Tangent * facing;
        Vector2 middle = sum / outline.Count;
        int center = Vertex(frame.Place(middle), normal, frame.Side, frame.Up, middle - min, size);
        int first = Vertex(frame.Place(outline[0] * scale), normal, frame.Side, frame.Up, outline[0] * scale - min, size);
        int previous = first;
        for (int i = 1; i < outline.Count; i++)
        {
            Vector2 p = outline[i] * scale;
            int current = Vertex(frame.Place(p), normal, frame.Side, frame.Up, p - min, size);
            Triangle(center, previous, current);
            previous = current;
        }

        Triangle(center, previous, first);
    }
}
