using System;
using System.Collections.Generic;
using System.Numerics;
using static TheDeep.DressingLayout;

namespace TheDeep;

/// <summary>A foundry's wall (<see cref="WallStyle.Foundry"/>): the rules over <see cref="FoundryWallSpec"/>.</summary>
public static class FoundryWall
{
    private const int Motif = 1, PipeCount = 2, PipeRadiusRoll = 3, ChainChanceRoll = 4, ChainLengthRoll = 5, ChainPlaceRoll = 6;

    /// <summary>The salt's stride along a bay's pipes (the index of a pipe's roll is the bay times this plus the pipe's).</summary>
    private const int PipeStride = 3;

    private static readonly FoundryWallSpec Spec = FoundryWallSpec.Default;

    /// <summary>The foundry wall's pieces for a wall <paramref name="width"/> by <paramref name="height"/> meters.</summary>
    public static List<WallPlacement> Build(float width, float height, int seed)
    {
        FoundryWallSpec s = Spec;
        var list = new List<WallPlacement>();
        List<float> xs = WallLayout.Pilasters(width);
        foreach (float x in xs)
        {
            list.Add(new WallPlacement(WallPiece.Pilaster, new Vector3(x, 0f, s.PilasterDepth), new Vector2(s.PilasterWidth, height)));
        }

        // Girders across the whole wall: one low, one high when the wall is tall.
        float low = MathF.Min(s.LowGirderMax, height * s.LowGirderShare);
        list.Add(new WallPlacement(WallPiece.Girder, new Vector3(0f, low, s.GirderDepth), new Vector2(width, s.GirderThickness)));
        if (height > s.TallWall)
        {
            list.Add(new WallPlacement(WallPiece.Girder, new Vector3(0f, height * s.HighGirderShare, s.GirderDepth), new Vector2(width, s.GirderThickness)));
        }

        for (int i = 0; i + 1 < xs.Count; i++)
        {
            float a = xs[i] + s.PilasterWidth * 0.5f, b = xs[i + 1] - s.PilasterWidth * 0.5f;
            float mid = (a + b) * 0.5f, span = b - a;
            float motif = Hash01(seed, Motif, i);
            if (motif < s.MouthChance && span > s.MouthMinSpan)
            {
                // A furnace mouth on the girder: molten metal behind a grate.
                float w = MathF.Min(span * s.MouthSpanShare, s.MouthMaxWidth);
                float h = MathF.Min(w * s.MouthAspect, MathF.Max(s.MouthMinHeight, height - low - s.MouthHeadroom));
                list.Add(new WallPlacement(WallPiece.MouthGlow, new Vector3(mid, low + s.MouthLift, s.GlowDepth), new Vector2(w, h)));
                list.Add(new WallPlacement(WallPiece.Mouth, new Vector3(mid, low + s.MouthLift, s.MouthDepth), new Vector2(w, h)));
            }
            else if (motif < s.PipeBelow)
            {
                // Pipes running up the bay, one or two.
                int n = 1 + (int)(Hash01(seed, PipeCount, i) * s.MaxPipes);
                for (int k = 0; k < n; k++)
                {
                    float px = a + span * (k + 1f) / (n + 1f);
                    float radius = s.PipeRadius + s.PipeRadiusSpread * Hash01(seed, PipeRadiusRoll, i * PipeStride + k);
                    list.Add(new WallPlacement(WallPiece.Pipe, new Vector3(px, 0f, s.PipeDepth), new Vector2(radius, height)));
                }
            }

            // Chains hanging from the top in some bays.
            if (Hash01(seed, ChainChanceRoll, i) < s.ChainChance)
            {
                float length = height * (s.ChainLengthShare + s.ChainLengthSpread * Hash01(seed, ChainLengthRoll, i));
                float at = a + span * (s.ChainAt + s.ChainAtSpread * Hash01(seed, ChainPlaceRoll, i));
                list.Add(new WallPlacement(WallPiece.Chain, new Vector3(at, height, s.ChainDepth), new Vector2(s.ChainWidth, length)));
            }
        }

        return list;
    }
}
