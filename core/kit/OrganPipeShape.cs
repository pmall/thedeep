using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Organ pipes as pure geometry (Godot-free, unit tested): a flue pipe standing on its toe, its mouth facing +Z. From the
/// bottom: a conical foot, the lower lip flattened into it as a point-down triangle, the mouth (a slot cut into the pipe,
/// its inner face leaning back under the upper lip and facing down, so it reads dark under light from above), the upper lip
/// flattened above it as a pointed shield, the round body, and an open top whose rim shows the pipe's wall around a dark bore.
/// </summary>
public static class OrganPipeShape
{
    private const int Sides = 32;

    /// <summary>Half the mouth's width as an angle from the front: a mouth a quarter of the girth wide.</summary>
    private const float MouthAngle = MathF.PI / 4f;

    /// <summary>
    /// A rank of pipes side by side along X, centered on the origin, <paramref name="spacing"/> meters apart, one per entry
    /// of <paramref name="heights"/>. The tallest is <paramref name="radius"/> wide; a shorter one is narrower, by the
    /// cube root of its height (organ pipes narrow more slowly than they shorten).
    /// </summary>
    public static void Rank(MeshBuilder b, IReadOnlyList<float> heights, float spacing, float radius)
    {
        float tallest = 0f;
        for (int i = 0; i < heights.Count; i++)
        {
            tallest = MathF.Max(tallest, heights[i]);
        }

        Matrix4x4 placement = b.Transform;
        for (int i = 0; i < heights.Count; i++)
        {
            float x = (i - (heights.Count - 1) * 0.5f) * spacing;
            b.Transform = Matrix4x4.CreateTranslation(x, 0f, 0f) * placement;
            b.Variation = (i * 0.618f + 0.15f) % 1f;
            Pipe(b, heights[i], radius * MathF.Cbrt(heights[i] / MathF.Max(tallest, 1e-3f)));
        }

        b.Transform = placement;
    }

    /// <summary>One pipe <paramref name="height"/> tall and <paramref name="radius"/> wide, standing on the origin.</summary>
    public static void Pipe(MeshBuilder b, float height, float radius)
    {
        float r = radius;
        float foot = Math.Clamp(height * 0.22f, 3f * r, height * 0.5f);
        float toe = r * 0.28f;
        float cutUp = r * 0.55f;
        float lowerLip = MathF.Min(r * 1.3f, foot * 0.6f);
        float upperLip = MathF.Min(r * 2.4f, (height - foot - cutUp) * 0.5f);
        float mouthBottom = foot, mouthTop = foot + cutUp;
        float halfMouth = r * MathF.Sin(MouthAngle), lipPlane = r * MathF.Cos(MouthAngle);
        float[] angles = Angles();
        float girth = MathF.Tau * r;

        // The foot: a cone from the toe, the lower lip flattened into its top.
        float[] footRows = Rows(new[] { (0f, foot, 7), (mouthBottom - lowerLip, mouthBottom, 6) });
        Surface(b, footRows, angles, height, girth, y =>
        {
            float rho = toe + (r - toe) * MathF.Min(1f, y / (foot * 0.92f));
            float t = (mouthBottom - y) / lowerLip;
            return (rho, t is >= 0f and <= 1f ? MathF.Min(halfMouth * (1f - t), rho * 0.999f) : 0f);
        });

        // The body: the upper lip flattened over the mouth, a pointed shield, then round to the top.
        float[] bodyRows = Rows(new[] { (mouthTop, mouthTop + upperLip, 9), (mouthTop + upperLip, height, 3) });
        Surface(b, bodyRows, angles, height, girth, y =>
        {
            float t = (y - mouthTop) / upperLip;
            return (r, t is >= 0f and < 1f ? halfMouth * (1f - t) * (1f + 0.6f * t) : 0f);
        });

        // Around the back of the mouth the pipe stays round.
        var back = new List<float>();
        foreach (float a in angles)
        {
            if (MathF.Abs(a) >= MouthAngle - 1e-4f)
            {
                back.Add(a < 0f ? a + MathF.Tau : a);
            }
        }

        back.Sort();
        var points = new Vector3[2, back.Count];
        var outward = new Vector3[2, back.Count];
        var uv = new Vector2[2, back.Count];
        for (int j = 0; j < 2; j++)
        {
            float y = j == 0 ? mouthBottom : mouthTop;
            for (int k = 0; k < back.Count; k++)
            {
                var radial = new Vector3(MathF.Sin(back[k]), 0f, MathF.Cos(back[k]));
                points[j, k] = radial * r + new Vector3(0f, y, 0f);
                outward[j, k] = radial;
                uv[j, k] = new Vector2(y, r * back[k]);
            }
        }

        b.Lattice(points, outward, uv, new Vector2(height, -girth), wraps: false);

        // The mouth: the slot's inner face, leaning back from the upper lip so it faces down into the dark, its sides, the
        // lower lip's ledge and the upper lip's edge.
        float deepBottom = lipPlane - r * 0.95f, deepTop = lipPlane - r * 0.2f;
        static Vector3 Corner(float x, float y, float z) => new(x, y, z);
        b.Cavity = 1f;
        FlatQuad(b, Corner(-halfMouth, mouthBottom, deepBottom), Corner(halfMouth, mouthBottom, deepBottom),
            Corner(halfMouth, mouthTop, deepTop), Corner(-halfMouth, mouthTop, deepTop),
            Vector3.Normalize(new Vector3(0f, deepBottom - deepTop, cutUp)));
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * halfMouth;
            FlatQuad(b, Corner(x, mouthBottom, deepBottom), Corner(x, mouthBottom, lipPlane),
                Corner(x, mouthTop, lipPlane), Corner(x, mouthTop, deepTop), new Vector3(-side, 0f, 0f));
        }

        foreach ((float y, float inner, float facing) in new[] { (mouthBottom, deepBottom, 1f), (mouthTop, deepTop, -1f) })
        {
            // The lower lip's ledge catches the light; the upper lip's edge is in the dark.
            b.Cavity = facing > 0f ? 0f : 1f;
            FlatQuad(b, Corner(-halfMouth, y, inner), Corner(halfMouth, y, inner),
                Corner(halfMouth, y, lipPlane), Corner(-halfMouth, y, lipPlane), new Vector3(0f, facing, 0f));
        }

        b.Cavity = 0f;
        Top(b, height, r, angles, girth);
    }

    /// <summary>The open top: the rim of the pipe's wall, the bore going down into the dark, and its floor.</summary>
    private static void Top(MeshBuilder b, float height, float r, float[] angles, float girth)
    {
        float inner = r - MathF.Max(0.015f, r * 0.08f);
        float bore = MathF.Min(r * 2.5f, height * 0.2f);
        int columns = angles.Length + 1;
        var rim = new Vector3[2, columns];
        var up = new Vector3[2, columns];
        var rimUv = new Vector2[2, columns];
        var wall = new Vector3[2, columns];
        var inward = new Vector3[2, columns];
        var wallUv = new Vector2[2, columns];
        for (int k = 0; k < columns; k++)
        {
            float a = angles[k % angles.Length];
            var radial = new Vector3(MathF.Sin(a), 0f, MathF.Cos(a));
            float v = r * (a + MathF.PI * 0.5f) + (k == angles.Length ? girth : 0f);
            rim[0, k] = radial * r + new Vector3(0f, height, 0f);
            rim[1, k] = radial * inner + new Vector3(0f, height, 0f);
            wall[0, k] = rim[1, k];
            wall[1, k] = radial * inner + new Vector3(0f, height - bore, 0f);
            up[0, k] = up[1, k] = Vector3.UnitY;
            inward[0, k] = inward[1, k] = -radial;
            rimUv[0, k] = new Vector2(0f, v);
            rimUv[1, k] = new Vector2(r - inner, v);
            wallUv[0, k] = new Vector2(height, v);
            wallUv[1, k] = new Vector2(height - bore, v);
        }

        b.Lattice(rim, up, rimUv, new Vector2(r - inner, -girth), wraps: true);
        b.Cavity = 0.85f;
        b.Lattice(wall, inward, wallUv, new Vector2(height, -girth), wraps: true);
        b.Cavity = 1f;
        var floor = SweepPath.Line(new Vector3(0f, height - bore - 0.01f, 0f), new Vector3(0f, height - bore, 0f), Vector3.UnitZ);
        b.Cap(floor[^1], Profile.Circle(inner, angles.Length), 1f);
        b.Cavity = 0f;
    }

    /// <summary>
    /// A ring surface through <paramref name="rows"/> (heights): <paramref name="shape"/> gives each row's radius and the
    /// half-width of the flat its front is pressed to (0 for none), so a lip's outline follows the rows.
    /// </summary>
    private static void Surface(MeshBuilder b, float[] rows, float[] angles, float height, float girth, Func<float, (float Radius, float Flat)> shape)
    {
        int columns = angles.Length + 1;
        var points = new Vector3[rows.Length, columns];
        var outward = new Vector3[rows.Length, columns];
        var uv = new Vector2[rows.Length, columns];
        for (int j = 0; j < rows.Length; j++)
        {
            float y = rows[j];
            (float rho, float flat) = shape(y);
            for (int k = 0; k < columns; k++)
            {
                float a = angles[k % angles.Length];
                float x = rho * MathF.Sin(a), z = rho * MathF.Cos(a);
                if (flat > 0f && MathF.Abs(x) < flat)
                {
                    z = MathF.Min(z, MathF.Sqrt(rho * rho - flat * flat));
                }

                points[j, k] = new Vector3(x, y, z);
                outward[j, k] = new Vector3(MathF.Sin(a), 0f, MathF.Cos(a));
                uv[j, k] = new Vector2(y, girth * (a + MathF.PI * 0.5f) / MathF.Tau + (k == angles.Length ? girth : 0f));
            }
        }

        b.Lattice(points, outward, uv, new Vector2(height, -girth), wraps: true);
    }

    /// <summary>
    /// The girth's angles (0 at the front, +π/2 at +X), even, with the mouth's edges added so the lips end on them. They
    /// start at the −X side, so the girth's seam, and the panel seams fitted to it, run down the pipe's flank, not its front.
    /// </summary>
    private static float[] Angles()
    {
        var angles = new List<float> { -MouthAngle, MouthAngle };
        for (int i = 0; i < Sides; i++)
        {
            float a = -MathF.PI * 0.5f + MathF.Tau * i / Sides;
            if (MathF.Abs(MathF.Abs(a) - MouthAngle) > 0.02f)
            {
                angles.Add(a);
            }
        }

        angles.Sort();
        return angles.ToArray();
    }

    /// <summary>Heights through each (from, to, count) stretch, sorted, near-duplicates merged.</summary>
    private static float[] Rows((float From, float To, int Count)[] stretches)
    {
        var rows = new List<float>();
        foreach ((float from, float to, int count) in stretches)
        {
            for (int i = 0; i < count; i++)
            {
                rows.Add(from + (to - from) * i / (count - 1));
            }
        }

        rows.Sort();
        var merged = new List<float>();
        foreach (float y in rows)
        {
            if (merged.Count == 0 || y - merged[^1] > 1e-3f)
            {
                merged.Add(y);
            }
        }

        return merged.ToArray();
    }

    private static void FlatQuad(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2, Vector3 d, Vector3 normal)
    {
        Vector3 u = c1 - a, v = d - a;
        float w = u.Length(), h = v.Length();
        var size = new Vector2(MathF.Max(w, 1e-3f), MathF.Max(h, 1e-3f));
        b.Quad(b.Vertex(a, normal, u, v, Vector2.Zero, size), b.Vertex(c1, normal, u, v, new Vector2(w, 0f), size),
            b.Vertex(c2, normal, u, v, new Vector2(w, h), size), b.Vertex(d, normal, u, v, new Vector2(0f, h), size));
    }
}
