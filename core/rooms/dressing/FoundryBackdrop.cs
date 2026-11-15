using System;
using System.Collections.Generic;
using System.Numerics;
using static TheDeep.DressingLayout;

namespace TheDeep;

/// <summary>A foundry backdrop (<see cref="BackdropStyle.Foundry"/>): the rules over <see cref="FoundryBackdropSpec"/>.</summary>
public static class FoundryBackdrop
{
    /// <summary>The salts of its decisions per furnace or gantry; the chains add their level to the last two, so each draws its own.</summary>
    private const int ExtraFurnace = 1, FurnaceTop = 2, ShortStack = 3, ShortStackTop = 4, LevelCount = 4, GantryHeight = 5, MouthLit = 6, TankPlace = 7, TankPosition = 8;

    private const int DuctPlace = 9, DuctHeight = 10, ChainPlace = 20, ChainLength = 30;

    /// <summary>The salt's stride along a gantry's panels (the index of a panel's roll is the bay times this plus the panel's).</summary>
    private const int PanelStride = 31;

    private static readonly FoundryBackdropSpec Spec = FoundryBackdropSpec.Default;

    /// <summary>The pieces of one row of the foundry.</summary>
    public static List<BackdropPlacement> Build(BackdropRow row)
    {
        FoundryBackdropSpec s = Spec;
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = BackdropLayout.Bays(row);
        float band = BackdropLayout.Band(row);
        var stack = new bool[xs.Count];
        var hoodTop = new float[xs.Count];
        for (int k = 0; k < xs.Count; k++)
        {
            // A furnace on every other bay line at least, its stack rising from its hood, some stacks shorter.
            stack[k] = k % 2 == 0 || Hash01(row.Seed, ExtraFurnace, k) < s.ExtraFurnaceChance;
            if (!stack[k])
            {
                continue;
            }

            // Built in whole steps of length, so each piece's mesh is made at the length it is placed at.
            int steps = Math.Max(1, (int)MathF.Round((row.FloorY + d.FurnaceHeight * (s.FurnaceHeightShare + s.FurnaceHeightSpread * Hash01(row.Seed, FurnaceTop, k)) - row.BaseY) / d.LengthStep));
            float top = row.BaseY + steps * d.LengthStep;
            list.Add(new BackdropPlacement(BackdropPiece.Furnace, new Vector3(xs[k], (row.BaseY + top) * 0.5f, 0f), Length: steps * d.LengthStep));
            list.Add(new BackdropPlacement(BackdropPiece.Hood, new Vector3(xs[k], top + d.HoodHeight * 0.5f, 0f)));
            hoodTop[k] = top + d.HoodHeight;
            float end = Hash01(row.Seed, ShortStack, k) < s.ShortStackChance ? hoodTop[k] + band * (s.ShortStackShare + s.ShortStackSpread * Hash01(row.Seed, ShortStackTop, k)) : row.TopY;
            int stackSteps = Math.Max(1, (int)MathF.Round((end - hoodTop[k]) / d.LengthStep));
            list.Add(new BackdropPlacement(BackdropPiece.Stack, new Vector3(xs[k], hoodTop[k], 0f), Length: stackSteps * d.LengthStep));
            if (row.Glows && Hash01(row.Seed, MouthLit, k) < s.MouthChance)
            {
                list.Add(new BackdropPlacement(BackdropPiece.FurnaceMouth, new Vector3(xs[k], top - d.FurnaceHeight * s.MouthDropShare, d.FurnaceDepth * s.MouthDepthShare)));
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

            Gantries(list, s, d, row, k, xs[k], xs[next], MathF.Max(hoodTop[k], hoodTop[next]));
        }

        return list;
    }

    /// <summary>The gantries between the stacks at <paramref name="left"/> and <paramref name="right"/>, and the duct over them.</summary>
    private static void Gantries(List<BackdropPlacement> list, FoundryBackdropSpec s, BackdropDims d, BackdropRow row, int k, float left, float right, float hoodsTop)
    {
        float from = left + d.StackRadius, to = right - d.StackRadius;
        float span = to - from;
        float mid = (from + to) * 0.5f;
        float low = hoodsTop + (s.GantryLift + s.GantryLiftSpread * Hash01(row.Seed, GantryHeight, k)) * row.Scale;
        int levels = Hash01(row.Seed, LevelCount, k) < s.OneLevelChance ? 1 : 2;
        float high = low;
        for (int level = 0; level < levels; level++)
        {
            low = level == 0 ? low : high + d.TankRadius * 2f + d.TrussDepth;
            high = low + d.TrussDepth;
            Truss(list, d, from, span, low, high);
            if (level == 0 && span > d.TankLength * s.TankFit && Hash01(row.Seed, TankPlace, k) < s.TankChance)
            {
                float x = mid + (Hash01(row.Seed, TankPosition, k) - 0.5f) * (span - d.TankLength * s.TankMargin);
                list.Add(new BackdropPlacement(BackdropPiece.Tank, new Vector3(x, high + d.BeamSection * 0.5f + d.TankRadius, 0f)));
            }

            int panels = Math.Max(1, (int)MathF.Round(span / d.TrussDepth));
            float w = span / panels;
            for (int c = 0; c < panels; c++)
            {
                if (Hash01(row.Seed, ChainPlace + level, k * PanelStride + c) < s.ChainChance)
                {
                    float chain = (low - row.FloorY) * (s.ChainLengthShare + s.ChainLengthSpread * Hash01(row.Seed, ChainLength + level, k * PanelStride + c));
                    list.Add(new BackdropPlacement(BackdropPiece.Chain, new Vector3(from + (c + 0.5f) * w, low, 0f), Length: chain));
                }
            }
        }

        if (Hash01(row.Seed, DuctPlace, k) < s.DuctChance)
        {
            // From one stack's axis to the next, its ends inside them.
            float duct = high + d.DuctRadius * 2f + (s.DuctLift + s.DuctLiftSpread * Hash01(row.Seed, DuctHeight, k)) * row.Scale;
            list.Add(new BackdropPlacement(BackdropPiece.Duct, new Vector3((left + right) * 0.5f, duct, 0f), Length: right - left));
        }
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
}
