using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>The architecture standing behind a room, by depth layer (a <see cref="BiomeProfile"/> choice).</summary>
public enum BackdropStyle
{
    /// <summary>Cathedral naves: clustered piers carrying pointed arches, slimmer arcaded tiers above (the flooded layer).</summary>
    Nave,

    /// <summary>Archive stacks: towers of racks, walkways between them fringed with icicles (the frozen layer).</summary>
    Stacks,

    /// <summary>A foundry: chimney stacks, trusses between them, chains hanging from the trusses (the volcanic layer).</summary>
    Foundry,

    /// <summary>Ancient machinery: great rings on hubs, spokes, pistons between them (the core).</summary>
    Machinery,
}

/// <summary>The pieces a backdrop row is built from; each style uses some of them.</summary>
public enum BackdropPiece
{
    Pier,
    UpperPier,
    PointedArch,
    UpperArch,
    Course,
    Rib,
    Tower,
    Walkway,
    Icicles,
    Chimney,
    Chord,
    Brace,
    Chain,
    Ring,
    Hub,
    Spoke,
    Piston,
}

/// <summary>
/// One piece placed in a backdrop row: where its origin goes (meters, the row's frame: X along the room, Y up, Z toward
/// the camera), its rotation about Z then X (radians), and its scale.
/// </summary>
public readonly record struct BackdropPlacement(BackdropPiece Piece, Vector3 Position, float RollZ = 0f, float PitchX = 0f, float Length = 1f);

/// <summary>
/// A row of backdrop architecture: the pieces' sizes for one depth (<see cref="Scale"/> grows with distance, so far rows
/// read as larger structures), the bay <see cref="Pitch"/>, and the span to fill: X from <see cref="MinX"/> to
/// <see cref="MaxX"/>, Y from <see cref="BaseY"/> (hidden in the mist) up past <see cref="TopY"/> (out of frame).
/// </summary>
public readonly record struct BackdropRow(float MinX, float MaxX, float BaseY, float TopY, float Pitch, float Scale, int Seed);

/// <summary>
/// Pure, deterministic layouts of the architecture behind a room (<see cref="BackdropArchitecture"/> turns them into meshes):
/// regular bays with the odd broken pier or fallen arch, so the backdrop reads as built structures receding into the
/// haze rather than scattered boxes. The piece sizes the layout assumes are in <see cref="BackdropDims"/>.
/// </summary>
public static class BackdropLayout
{
    public static List<BackdropPlacement> Build(BackdropStyle style, BackdropRow row) => style switch
    {
        BackdropStyle.Stacks => Stacks(row),
        BackdropStyle.Foundry => Foundry(row),
        BackdropStyle.Machinery => Machinery(row),
        _ => Nave(row),
    };

    /// <summary>The bay lines of a row: X of each pier, a seeded phase so rows don't line up.</summary>
    public static List<float> Bays(BackdropRow row)
    {
        var xs = new List<float>();
        float phase = DressingLayout.Hash01(row.Seed, 999) * row.Pitch;
        for (float x = row.MinX - phase; x <= row.MaxX + row.Pitch; x += row.Pitch)
        {
            xs.Add(x);
        }

        return xs;
    }

    private static float H(BackdropRow row, int a, int b) => DressingLayout.Hash01(row.Seed + a * 7919, b);

    private static List<BackdropPlacement> Nave(BackdropRow row)
    {
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = Bays(row);

        // The tiers' bases: a tall arcade, then round-arched tiers until out of frame.
        var tiers = new List<float> { row.BaseY };
        float y = row.BaseY + d.PierHeight + d.PointedRise + d.CourseHeight;
        while (y < row.TopY)
        {
            tiers.Add(y);
            y += d.UpperHeight + d.UpperRise + d.CourseHeight;
        }

        // How many tiers each pier still has: most all of them, some broken off.
        var height = new int[xs.Count];
        for (int k = 0; k < xs.Count; k++)
        {
            height[k] = H(row, 1, k) < 0.2f && tiers.Count > 1 ? 1 + (int)(H(row, 2, k) * (tiers.Count - 1)) : tiers.Count;
        }

        for (int k = 0; k < xs.Count; k++)
        {
            for (int j = 0; j < height[k]; j++)
            {
                list.Add(new BackdropPlacement(j == 0 ? BackdropPiece.Pier : BackdropPiece.UpperPier, new Vector3(xs[k], tiers[j], 0f)));
            }
        }

        for (int k = 0; k + 1 < xs.Count; k++)
        {
            float mid = (xs[k] + xs[k + 1]) * 0.5f;
            int both = Math.Min(height[k], height[k + 1]);
            for (int j = 0; j < both; j++)
            {
                if (H(row, 3 + j, k) < 0.1f)
                {
                    continue; // a fallen arch
                }

                bool first = j == 0;
                float spring = tiers[j] + (first ? d.PierHeight : d.UpperHeight);
                list.Add(new BackdropPlacement(first ? BackdropPiece.PointedArch : BackdropPiece.UpperArch, new Vector3(mid, spring, 0f)));
                float course = spring + (first ? d.PointedRise : d.UpperRise) + d.CourseHeight * 0.5f;
                list.Add(new BackdropPlacement(BackdropPiece.Course, new Vector3(mid, course, 0f)));
                if (first && H(row, 40, k) < 0.14f)
                {
                    // An iron rib in front of the arch: the accent, sparse.
                    list.Add(new BackdropPlacement(BackdropPiece.Rib, new Vector3(mid, spring, d.ArchDepth * 0.5f + 0.2f * row.Scale)));
                }
            }
        }

        return list;
    }

    private static List<BackdropPlacement> Stacks(BackdropRow row)
    {
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = Bays(row);
        var floors = new int[xs.Count];
        for (int k = 0; k < xs.Count; k++)
        {
            // Towers of stacked rack segments, of uneven height; the odd one missing.
            if (H(row, 1, k) < 0.12f)
            {
                floors[k] = 0;
                continue;
            }

            float top = row.TopY - (row.TopY - row.BaseY) * 0.45f * H(row, 2, k);
            int n = Math.Max(1, (int)MathF.Ceiling((top - row.BaseY) / d.TowerHeight));
            floors[k] = n;
            for (int j = 0; j < n; j++)
            {
                list.Add(new BackdropPlacement(BackdropPiece.Tower, new Vector3(xs[k], row.BaseY + j * d.TowerHeight, 0f)));
            }
        }

        for (int k = 0; k + 1 < xs.Count; k++)
        {
            int both = Math.Min(floors[k], floors[k + 1]);
            float mid = (xs[k] + xs[k + 1]) * 0.5f;
            for (int j = 1; j < both; j++)
            {
                if (H(row, 3 + j, k) < 0.55f)
                {
                    continue;
                }

                float at = row.BaseY + j * d.TowerHeight;
                list.Add(new BackdropPlacement(BackdropPiece.Walkway, new Vector3(mid, at, 0f)));
                list.Add(new BackdropPlacement(BackdropPiece.Icicles, new Vector3(mid, at - d.WalkwayHeight * 0.5f, d.WalkwayDepth * 0.3f)));
            }
        }

        return list;
    }

    private static List<BackdropPlacement> Foundry(BackdropRow row)
    {
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = Bays(row);
        var stack = new bool[xs.Count];
        for (int k = 0; k < xs.Count; k++)
        {
            // Every other bay at least: a chimney, some broken short.
            stack[k] = k % 2 == 0 || H(row, 1, k) < 0.3f;
            if (stack[k])
            {
                float top = H(row, 2, k) < 0.25f ? row.BaseY + (row.TopY - row.BaseY) * (0.45f + 0.3f * H(row, 3, k)) : row.TopY + 6f * row.Scale;
                list.Add(new BackdropPlacement(BackdropPiece.Chimney, new Vector3(xs[k], row.BaseY, 0f), Length: top - row.BaseY));
            }
        }

        // Trusses between neighbouring chimneys at one or two heights, chains hanging from them.
        for (int k = 0; k < xs.Count; k++)
        {
            int next = k + 1;
            while (next < xs.Count && !stack[next])
            {
                next++;
            }

            if (!stack[k] || next >= xs.Count)
            {
                continue;
            }

            float from = xs[k] + d.ChimneyRadius, to = xs[next] - d.ChimneyRadius;
            float span = to - from;
            float mid = (from + to) * 0.5f;
            int levels = H(row, 4, k) < 0.5f ? 1 : 2;
            for (int level = 0; level < levels; level++)
            {
                float low = row.BaseY + (row.TopY - row.BaseY) * (0.3f + 0.3f * level + 0.1f * H(row, 5 + level, k));
                float high = low + d.TrussDepth;
                list.Add(new BackdropPlacement(BackdropPiece.Chord, new Vector3(mid, low, 0f), Length: span));
                list.Add(new BackdropPlacement(BackdropPiece.Chord, new Vector3(mid, high, 0f), Length: span));
                int panels = Math.Max(1, (int)MathF.Round(span / d.TrussDepth));
                float w = span / panels;
                float angle = MathF.Atan2(d.TrussDepth, w);
                float length = MathF.Sqrt(w * w + d.TrussDepth * d.TrussDepth);
                for (int p = 0; p < panels; p++)
                {
                    float cx = from + (p + 0.5f) * w;
                    list.Add(new BackdropPlacement(BackdropPiece.Brace, new Vector3(cx, (low + high) * 0.5f, 0f), RollZ: p % 2 == 0 ? angle : -angle, Length: length));
                }

                for (int c = 0; c < panels; c++)
                {
                    if (H(row, 20 + level, k * 31 + c) < 0.22f)
                    {
                        float chain = (low - row.BaseY) * (0.25f + 0.5f * H(row, 30 + level, k * 31 + c));
                        list.Add(new BackdropPlacement(BackdropPiece.Chain, new Vector3(from + (c + 0.5f) * w, low, 0f), Length: chain));
                    }
                }
            }
        }

        return list;
    }

    private static List<BackdropPlacement> Machinery(BackdropRow row)
    {
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = Bays(row);
        for (int k = 0; k < xs.Count; k++)
        {
            // A piston column on every bay line; a great ring on a hub every other bay, at an uneven height.
            list.Add(new BackdropPlacement(BackdropPiece.Piston, new Vector3(xs[k], row.BaseY, 0f), Length: row.TopY + 6f * row.Scale - row.BaseY));
            if (k % 2 == 1 || k + 1 >= xs.Count)
            {
                continue;
            }

            float cx = (xs[k] + xs[k + 1]) * 0.5f;
            float cy = row.BaseY + (row.TopY - row.BaseY) * (0.35f + 0.35f * H(row, 1, k));
            float radius = d.RingRadius * (0.75f + 0.4f * H(row, 2, k));
            // In front of the pistons, so they pass behind the wheel.
            float z = 2f * row.Scale;
            list.Add(new BackdropPlacement(BackdropPiece.Ring, new Vector3(cx, cy, z), PitchX: MathF.PI * 0.5f, Length: radius));
            list.Add(new BackdropPlacement(BackdropPiece.Hub, new Vector3(cx, cy, z - 0.6f * row.Scale), PitchX: MathF.PI * 0.5f));
            int spokes = 4 + (int)(H(row, 3, k) * 3f);
            float turn = H(row, 4, k) * MathF.PI;
            for (int s = 0; s < spokes; s++)
            {
                float a = turn + MathF.PI * s / spokes;
                list.Add(new BackdropPlacement(BackdropPiece.Spoke, new Vector3(cx, cy, z), RollZ: a, Length: radius * 2f));
            }
        }

        return list;
    }
}

/// <summary>The sizes of a row's pieces, from its pitch and scale (shared by the layout and the meshes built for it).</summary>
public readonly record struct BackdropDims(float Pitch, float Scale)
{
    public static BackdropDims For(BackdropRow row) => new(row.Pitch, row.Scale);

    public float PierHeight => 9f * Scale;
    public float PierRadius => 0.9f * Scale;
    public float UpperHeight => 7.5f * Scale;
    public float PointedRise => 0.62f * Pitch;

    /// <summary>The upper tiers' arches: pointed too (round ones stacked read as a lattice of rings), a little lower.</summary>
    public float UpperRise => 0.56f * Pitch;
    public float CourseHeight => 0.7f * Scale;
    public float ArchThickness => 0.8f * Scale;
    public float ArchDepth => 1.6f * Scale;

    public float TowerHeight => 6f * Scale;
    public float TowerWidth => 0.5f * Pitch;
    public float WalkwayHeight => 0.5f * Scale;
    public float WalkwayDepth => 2f * Scale;

    public float ChimneyRadius => 1.3f * Scale;
    public float TrussDepth => 1.8f * Scale;
    public float BeamSection => 0.3f * Scale;

    public float RingRadius => 0.85f * Pitch;
    public float PistonRadius => 0.45f * Scale;
}
