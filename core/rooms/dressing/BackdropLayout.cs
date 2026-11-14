using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>The architecture standing behind a room, by depth layer (a <see cref="BiomeProfile"/> choice).</summary>
public enum BackdropStyle
{
    /// <summary>Cathedral naves: clustered piers carrying pointed arches, slimmer arcaded tiers above, a few shafts running
    /// up the piers' faces (the flooded layer).</summary>
    Nave,

    /// <summary>Archive stacks: towers of racks, walkways between them fringed with icicles (the frozen layer).</summary>
    Stacks,

    /// <summary>A foundry: furnaces with their stacks, gantries and ducts between the stacks, tanks on the gantries, chains
    /// hanging from them, a few furnace mouths glowing in the farthest row (the volcanic layer).</summary>
    Foundry,

    /// <summary>Ancient machinery: great gears on their axles, pistons between them (the core).</summary>
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

    /// <summary>A slender shaft running up a nave pier's face from its springing to its top tier.</summary>
    Shaft,
    Tower,
    Walkway,
    Icicles,

    /// <summary>A furnace's body, from the row's foot up past the room's floor.</summary>
    Furnace,

    /// <summary>The narrower hood on a furnace that its stack rises from.</summary>
    Hood,

    /// <summary>A furnace's mouth on its front face: the only light of the foundry's backdrop.</summary>
    FurnaceMouth,

    /// <summary>A banded stack rising from a hood.</summary>
    Stack,

    /// <summary>A horizontal duct from one stack to the next.</summary>
    Duct,

    /// <summary>A banded tank lying on a gantry.</summary>
    Tank,
    Chord,
    Brace,
    Chain,

    /// <summary>A great toothed gear facing the camera, its back on its origin's plane; its length is its radius.</summary>
    Gear,
    Hub,
    Piston,
}

/// <summary>
/// One piece placed in a backdrop row: where its origin goes (meters, the row's frame: X along the room, Y up, Z toward
/// the camera), its rotation about Z then X (radians), and its scale.
/// </summary>
public readonly record struct BackdropPlacement(BackdropPiece Piece, Vector3 Position, float RollZ = 0f, float PitchX = 0f, float Length = 1f);

/// <summary>
/// A row of backdrop architecture: the pieces' sizes for one depth (<see cref="Scale"/> grows with distance, so far rows
/// read as larger structures), the bay <see cref="Pitch"/>, the span to fill (X from <see cref="MinX"/> to
/// <see cref="MaxX"/>, Y from its foot <see cref="BaseY"/> to its top <see cref="TopY"/>, both off screen) and the band the
/// room's camera bounds span (<see cref="FloorY"/> to <see cref="RoofY"/>), where a style puts what should be seen. Its
/// furnace mouths glow only where <see cref="Glows"/> (the farthest row). Made by <see cref="BackdropRowSpec.Behind"/>.
/// </summary>
public readonly record struct BackdropRow(float MinX, float MaxX, float BaseY, float TopY, float FloorY, float RoofY, float Pitch, float Scale, int Seed, bool Glows = false);

/// <summary>
/// Pure, deterministic layouts of the architecture behind a room (<see cref="BackdropArchitecture"/> turns them into meshes):
/// regular bays with the odd broken pier or fallen arch, so the backdrop reads as built structures receding into the
/// haze rather than scattered boxes. The piece sizes the layout assumes are in <see cref="BackdropDims"/>.
/// </summary>
public static class BackdropLayout
{
    /// <summary>The pieces of one row of the given style, placed in the row's frame; the same row always gives the same
    /// pieces.</summary>
    public static List<BackdropPlacement> Build(BackdropStyle style, BackdropRow row) => style switch
    {
        BackdropStyle.Nave => Nave(row),
        BackdropStyle.Stacks => Stacks(row),
        BackdropStyle.Foundry => Foundry(row),
        BackdropStyle.Machinery => Machinery(row),
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "no layout for this backdrop style"),
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

    /// <summary>The band a style places its features in: the room's bounds, at least a few bays' worth tall.</summary>
    private static float Band(BackdropRow row) => MathF.Max(row.RoofY - row.FloorY, 10f * row.Scale);

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

            if (height[k] > 1 && H(row, 40, k) < 0.2f)
            {
                // A shaft up the pier's face, from the first arches' springing to its top: upright, few, in the stone.
                float from = tiers[0] + d.PierHeight;
                float to = tiers[height[k] - 1] + d.UpperHeight;
                list.Add(new BackdropPlacement(BackdropPiece.Shaft, new Vector3(xs[k], (from + to) * 0.5f, d.PierRadius + d.ShaftSection * 0.5f), Length: to - from));
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

            float top = row.TopY - (row.TopY - row.FloorY) * 0.45f * H(row, 2, k);
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
        float band = Band(row);
        var stack = new bool[xs.Count];
        var hoodTop = new float[xs.Count];
        for (int k = 0; k < xs.Count; k++)
        {
            // A furnace on every other bay line at least, its stack rising from its hood, some stacks shorter.
            stack[k] = k % 2 == 0 || H(row, 1, k) < 0.25f;
            if (!stack[k])
            {
                continue;
            }

            // Built in whole steps of length, so each piece's mesh is made at the length it is placed at.
            int steps = Math.Max(1, (int)MathF.Round((row.FloorY + d.FurnaceHeight * (0.6f + 0.8f * H(row, 2, k)) - row.BaseY) / d.LengthStep));
            float top = row.BaseY + steps * d.LengthStep;
            list.Add(new BackdropPlacement(BackdropPiece.Furnace, new Vector3(xs[k], (row.BaseY + top) * 0.5f, 0f), Length: steps * d.LengthStep));
            list.Add(new BackdropPlacement(BackdropPiece.Hood, new Vector3(xs[k], top + d.HoodHeight * 0.5f, 0f)));
            hoodTop[k] = top + d.HoodHeight;
            float end = H(row, 3, k) < 0.25f ? hoodTop[k] + band * (0.5f + 0.4f * H(row, 4, k)) : row.TopY;
            int stackSteps = Math.Max(1, (int)MathF.Round((end - hoodTop[k]) / d.LengthStep));
            list.Add(new BackdropPlacement(BackdropPiece.Stack, new Vector3(xs[k], hoodTop[k], 0f), Length: stackSteps * d.LengthStep));
            if (row.Glows && H(row, 6, k) < 0.4f)
            {
                list.Add(new BackdropPlacement(BackdropPiece.FurnaceMouth, new Vector3(xs[k], top - d.FurnaceHeight * 0.4f, d.FurnaceDepth * 0.5f)));
            }
        }

        // Gantries between neighbouring stacks over their hoods, at one or two heights: a tank on some, chains hanging from
        // them, a duct above.
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

            float from = xs[k] + d.StackRadius, to = xs[next] - d.StackRadius;
            float span = to - from;
            float mid = (from + to) * 0.5f;
            float low = MathF.Max(hoodTop[k], hoodTop[next]) + (2f + 4f * H(row, 5, k)) * row.Scale;
            int levels = H(row, 4, k) < 0.5f ? 1 : 2;
            float high = low;
            for (int level = 0; level < levels; level++)
            {
                low = level == 0 ? low : high + d.TankRadius * 2f + d.TrussDepth;
                high = low + d.TrussDepth;
                Truss(list, d, from, span, low, high);
                if (level == 0 && span > d.TankLength * 1.4f && H(row, 7, k) < 0.6f)
                {
                    float x = mid + (H(row, 8, k) - 0.5f) * (span - d.TankLength * 1.2f);
                    list.Add(new BackdropPlacement(BackdropPiece.Tank, new Vector3(x, high + d.BeamSection * 0.5f + d.TankRadius, 0f)));
                }

                int panels = Math.Max(1, (int)MathF.Round(span / d.TrussDepth));
                float w = span / panels;
                for (int c = 0; c < panels; c++)
                {
                    if (H(row, 20 + level, k * 31 + c) < 0.12f)
                    {
                        float chain = (low - row.FloorY) * (0.2f + 0.4f * H(row, 30 + level, k * 31 + c));
                        list.Add(new BackdropPlacement(BackdropPiece.Chain, new Vector3(from + (c + 0.5f) * w, low, 0f), Length: chain));
                    }
                }
            }

            if (H(row, 9, k) < 0.5f)
            {
                // From one stack's axis to the next, its ends inside them.
                float duct = high + d.DuctRadius * 2f + (3f + 3f * H(row, 10, k)) * row.Scale;
                list.Add(new BackdropPlacement(BackdropPiece.Duct, new Vector3((xs[k] + xs[next]) * 0.5f, duct, 0f), Length: xs[next] - xs[k]));
            }
        }

        return list;
    }

    /// <summary>A truss from <paramref name="from"/> along <paramref name="span"/>: two chords and braces zigzagging between.</summary>
    private static void Truss(List<BackdropPlacement> list, BackdropDims d, float from, float span, float low, float high)
    {
        float mid = from + span * 0.5f;
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
    }

    private static List<BackdropPlacement> Machinery(BackdropRow row)
    {
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = Bays(row);
        float band = Band(row);
        for (int k = 0; k < xs.Count; k++)
        {
            // A piston column on every bay line; a great gear on its axle every other bay, at an uneven height.
            list.Add(new BackdropPlacement(BackdropPiece.Piston, new Vector3(xs[k], row.BaseY, 0f), Length: row.TopY - row.BaseY));
            if (k % 2 == 1 || k + 1 >= xs.Count)
            {
                continue;
            }

            float cx = (xs[k] + xs[k + 1]) * 0.5f;
            float cy = row.FloorY + band * (0.3f + 0.4f * H(row, 1, k));
            float radius = d.GearRadius * (0.75f + 0.4f * H(row, 2, k));
            // In front of the pistons, so they pass behind the gear.
            float z = 2f * row.Scale;
            list.Add(new BackdropPlacement(BackdropPiece.Gear, new Vector3(cx, cy, z), RollZ: H(row, 4, k) * MathF.PI, Length: radius));
            list.Add(new BackdropPlacement(BackdropPiece.Hub, new Vector3(cx, cy, z - 0.6f * row.Scale), PitchX: MathF.PI * 0.5f));
        }

        return list;
    }
}

/// <summary>The sizes of a row's pieces, from its pitch and scale (shared by the layout and the meshes built for it).</summary>
public readonly record struct BackdropDims(float Pitch, float Scale)
{
    /// <summary>The sizes of the pieces of <paramref name="row"/>.</summary>
    public static BackdropDims For(BackdropRow row) => new(row.Pitch, row.Scale);

    /// <summary>The step, meters, that the pieces built at their length (stacks, furnaces, ducts, pistons) are made in:
    /// one mesh serves every piece of a length.</summary>
    public float LengthStep => 2f * Scale;

    /// <summary>A nave pier's height, from the row's base to where its arch springs, meters.</summary>
    public float PierHeight => 9f * Scale;
    /// <summary>A nave pier's radius, meters.</summary>
    public float PierRadius => 0.9f * Scale;
    /// <summary>The height of an upper tier's pier, slimmer and shorter than the first tier's, meters.</summary>
    public float UpperHeight => 7.5f * Scale;
    /// <summary>How far a first-tier arch rises above its springing line, meters; over half the bay it spans, so it is
    /// pointed.</summary>
    public float PointedRise => 0.62f * Pitch;

    /// <summary>The upper tiers' arches: pointed too (round ones stacked read as a lattice of rings), a little lower.</summary>
    public float UpperRise => 0.56f * Pitch;
    /// <summary>The height of the stone course that runs over a tier's arches, meters.</summary>
    public float CourseHeight => 0.7f * Scale;
    /// <summary>The thickness of an arch's ring, in the plane of the row, meters.</summary>
    public float ArchThickness => 0.8f * Scale;
    /// <summary>An arch's depth toward the camera, meters.</summary>
    public float ArchDepth => 1.6f * Scale;
    /// <summary>The side of a shaft's square section up a pier's face, meters.</summary>
    public float ShaftSection => 0.4f * Scale;

    /// <summary>The height of one tier of an archive tower, meters; tiers stack up out of frame.</summary>
    public float TowerHeight => 6f * Scale;
    /// <summary>An archive tower's width along the row, meters; the walkways span the rest of the bay.</summary>
    public float TowerWidth => 0.5f * Pitch;
    /// <summary>A walkway's thickness, meters.</summary>
    public float WalkwayHeight => 0.5f * Scale;
    /// <summary>A walkway's depth toward the camera, meters.</summary>
    public float WalkwayDepth => 2f * Scale;

    /// <summary>A furnace's width along the row, meters.</summary>
    public float FurnaceWidth => 0.6f * Pitch;
    /// <summary>A furnace's depth toward the camera, meters.</summary>
    public float FurnaceDepth => 4f * Scale;
    /// <summary>How far a furnace rises over the room's floor, meters: each between 0.6 and 1.4 times it.</summary>
    public float FurnaceHeight => 6f * Scale;
    /// <summary>A furnace hood's width, meters.</summary>
    public float HoodWidth => 0.36f * Pitch;
    /// <summary>A furnace hood's height, meters.</summary>
    public float HoodHeight => 2.4f * Scale;
    /// <summary>A furnace mouth's width and height, meters: small, far back.</summary>
    public float MouthWidth => 1.4f * Scale;
    public float MouthHeight => 0.8f * Scale;
    /// <summary>A stack's radius, meters; a gantry runs between two stacks' sides.</summary>
    public float StackRadius => 1.1f * Scale;
    /// <summary>A duct's radius, meters.</summary>
    public float DuctRadius => 0.5f * Scale;
    /// <summary>A tank's radius and length, meters.</summary>
    public float TankRadius => 1.5f * Scale;
    public float TankLength => 0.5f * Pitch;
    /// <summary>A truss's height from its lower chord to its upper one, meters; its panels are about as long.</summary>
    public float TrussDepth => 1.8f * Scale;
    /// <summary>The side of a truss chord's square section, meters; a brace is thinner.</summary>
    public float BeamSection => 0.3f * Scale;

    /// <summary>A machinery gear's mean radius, meters; each gear takes between 0.75 and 1.15 times it.</summary>
    public float GearRadius => 0.85f * Pitch;
    /// <summary>A piston's radius, meters.</summary>
    public float PistonRadius => 0.45f * Scale;
}
