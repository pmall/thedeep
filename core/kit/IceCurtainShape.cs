using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A curtain of ice as pure geometry (Godot-free, unit tested): water that poured over an edge and froze on the way down.
/// Rounded flutes side by side, joined by a thinner sheet, thickening as they go down (ice builds up where it runs),
/// rippled with frozen drips; each flute ends at its own length in a rounded point, the sheet between them higher (as far
/// as the sheet's share of the length), and the outer ones shorter, so the curtain's foot is rounded rather than square.
/// The sheet's hem is broken, not hemmed: it wanders, narrow icicles of scattered lengths hang from it between the flutes,
/// and each shoulder falls away over its own width, so ice over a lip never reads as a hung cloth.
/// </summary>
public static class IceCurtainShape
{
    private const float ColumnStep = 0.15f;
    private const float RowStep = 0.35f;

    /// <summary>
    /// A curtain hanging from y = 0 along X over <paramref name="width"/> meters (centered), its longest flute
    /// <paramref name="length"/> down, its back on the plane z = 0 and its front bulging toward +Z by up to
    /// <paramref name="depth"/>; different for each <paramref name="seed"/>. <paramref name="sheet"/> is the share of the
    /// length the sheet between the flutes reaches: high for a fall frozen whole, low for drips frozen over a lip. Only the
    /// front and the top are built: hang it against a wall or a slope.
    /// </summary>
    public static void Build(MeshBuilder b, float width, float length, float depth, int seed, float sheet = 0.62f)
    {
        var random = new Random(seed);
        float R(float min, float max) => min + (max - min) * (float)random.NextDouble();
        int flutes = Math.Max(2, (int)MathF.Round(width / 1.1f));
        float spacing = width / flutes;
        var center = new float[flutes];
        var half = new float[flutes];
        var swell = new float[flutes];
        var reach = new float[flutes];
        var phase = new float[flutes];
        for (int f = 0; f < flutes; f++)
        {
            center[f] = -width * 0.5f + spacing * (f + 0.5f + R(-0.25f, 0.25f));
            half[f] = spacing * R(0.6f, 1.05f);
            swell[f] = R(0.5f, 1f);
            reach[f] = R(0.7f, 1f);
            phase[f] = R(0f, MathF.Tau);
        }

        int columns = Math.Max(8, (int)MathF.Ceiling(width / ColumnStep)) + 1;
        int rows = Math.Max(8, (int)MathF.Ceiling(length / RowStep)) + 1;
        var points = new Vector3[rows, columns];
        var outward = new Vector3[rows, columns];
        var uv = new Vector2[rows, columns];
        b.Variation = (float)random.NextDouble();

        // Icicles between the flutes: narrow, each its own length past the sheet's hem.
        int icicles = Math.Max(3, (int)MathF.Round(width / 0.35f));
        var icicleAt = new float[icicles];
        var icicleHalf = new float[icicles];
        var icicleReach = new float[icicles];
        for (int i = 0; i < icicles; i++)
        {
            icicleAt[i] = -width * 0.5f + width * (i + 0.5f + R(-0.4f, 0.4f)) / icicles;
            icicleHalf[i] = R(0.16f, 0.3f);
            float t = R(0f, 1f);
            icicleReach[i] = sheet + (1f - sheet) * 0.8f * t * t;
        }

        // Over a lip (a low sheet) the flutes end in narrower points, leaving room for the icicles between them.
        float tipShare = 0.6f * Math.Clamp(0.55f + 0.45f * sheet / 0.62f, 0.55f, 1f);

        // The shoulders: each side falls away over its own width; the hem wanders.
        float shoulderLeft = width * R(0.12f, 0.3f), shoulderRight = width * R(0.12f, 0.3f);
        float hemPhase = R(0f, MathF.Tau), hemPhase2 = R(0f, MathF.Tau);

        // How far down each column reaches (a share of the length, the longest made 1) and how thick it is across.
        var bottoms = new float[columns];
        var sections = new float[columns];
        float longest = 0f;
        for (int k = 0; k < columns; k++)
        {
            float x = X(k);
            float side = x / (width * 0.5f);
            float taper = 1f - 0.85f * MathF.Pow(side * side, 2f);
            float shoulder = MathF.Sqrt(SmoothStep(0f, x < 0f ? shoulderLeft : shoulderRight, width * 0.5f - MathF.Abs(x)));
            float hem = 0.8f + 0.2f * MathF.Sin(x * 2.3f + hemPhase) + 0.12f * MathF.Sin(x * 6.1f + hemPhase2);
            bottoms[k] = MathF.Max(0.02f, sheet * taper * shoulder * hem);
            for (int i = 0; i < icicles; i++)
            {
                float tip = MathF.Max(0f, 1f - Square((x - icicleAt[i]) / icicleHalf[i]));
                bottoms[k] = MathF.Max(bottoms[k], icicleReach[i] * taper * shoulder * MathF.Pow(tip, 0.5f));
                sections[k] = MathF.Max(sections[k], 0.6f * MathF.Sqrt(tip));
            }

            for (int f = 0; f < flutes; f++)
            {
                float bump = Bump(x, f);
                // Each flute's tip is narrower than the flute, so neighbors part before they end.
                float tip = MathF.Max(0f, 1f - Square((x - center[f]) / (half[f] * tipShare)));
                bottoms[k] = MathF.Max(bottoms[k], reach[f] * taper * shoulder * MathF.Pow(tip, 0.4f));
                sections[k] = MathF.Max(sections[k], swell[f] * MathF.Sqrt(bump));
            }

            longest = MathF.Max(longest, bottoms[k]);
        }

        for (int k = 0; k < columns; k++)
        {
            float x = X(k), bottom = bottoms[k] / longest, section = sections[k];
            // Thin at the sides, where the curtain meets the rock.
            float edge = SmoothStep(0f, MathF.Min(0.8f, width * 0.15f), width * 0.5f - MathF.Abs(x));
            for (int j = 0; j < rows; j++)
            {
                float s = j / (float)(rows - 1);
                float y = -bottom * length * s;
                float drips = 0f;
                for (int f = 0; f < flutes; f++)
                {
                    drips += Bump(x, f) * MathF.Sin(-y * 1.9f + phase[f]);
                }

                float tip = s < 0.8f ? 1f : MathF.Sqrt(MathF.Max(0f, 1f - Square((s - 0.8f) / 0.2f)));
                float z = depth * (0.22f + 0.78f * section) * (0.5f + 0.5f * s) * (1f + 0.12f * drips) * tip * edge;
                points[j, k] = new Vector3(x, y, z);
                outward[j, k] = Vector3.UnitZ;
                uv[j, k] = new Vector2(-y, x + width * 0.5f);
            }
        }

        b.Lattice(points, outward, uv, new Vector2(length, width), wraps: false);

        // The top, closed back to the plane it hangs against (seen from above, over a ledge).
        var top = new Vector3[2, columns];
        var up = new Vector3[2, columns];
        var topUv = new Vector2[2, columns];
        for (int k = 0; k < columns; k++)
        {
            top[0, k] = points[0, k] with { Z = 0f };
            top[1, k] = points[0, k];
            up[0, k] = up[1, k] = Vector3.UnitY;
            topUv[0, k] = new Vector2(0f, points[0, k].X + width * 0.5f);
            topUv[1, k] = new Vector2(points[0, k].Z, points[0, k].X + width * 0.5f);
        }

        b.Lattice(top, up, topUv, new Vector2(depth, width), wraps: false);

        float X(int k) => -width * 0.5f + width * k / (columns - 1);
        float Bump(float x, int f) => MathF.Max(0f, 1f - Square((x - center[f]) / half[f]));
    }

    private static float Square(float v) => v * v;

    private static float SmoothStep(float from, float to, float v)
    {
        float t = Math.Clamp((v - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
