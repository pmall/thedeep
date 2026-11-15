using System;
using System.Collections.Generic;
using System.Numerics;
using static TheDeep.DressingLayout;

namespace TheDeep;

/// <summary>A nave's wall (<see cref="WallStyle.Nave"/>): the rules over <see cref="NaveWallSpec"/>.</summary>
public static class NaveWall
{
    /// <summary>The salts of its decisions; the tier's number is added to the last three, so every tier draws its own.</summary>
    private const int FallenArch = 1, RoseChance = 2, RoseTurn = 3, WalledUp = 10, FrostChance = 20, FrostLength = 30;

    /// <summary>The salt's stride along a tier's lancets (the index of a lancet's roll is the bay times this plus the lancet's).</summary>
    private const int LancetStride = 7;

    private static readonly NaveWallSpec Spec = NaveWallSpec.Default;

    /// <summary>
    /// The nave wall's pieces for a wall <paramref name="width"/> by <paramref name="height"/> meters, with
    /// <paramref name="frost"/> (0 to 1) the share of bays that have icicles under their courses.
    /// </summary>
    public static List<WallPlacement> Build(float width, float height, int seed, float frost)
    {
        NaveWallSpec s = Spec;
        var list = new List<WallPlacement>();
        List<float> xs = WallLayout.Pilasters(width);
        float gap = xs.Count > 1 ? xs[1] - xs[0] : width;
        list.Add(new WallPlacement(WallPiece.Plinth, new Vector3(0f, 0f, s.PlinthDepth), new Vector2(width, s.PlinthHeight)));

        // The arcade: as tall as the wall allows under a pointed arch, its course and a margin.
        float span = gap - s.ArcadePilaster + s.ArchRing;
        float rise = span * s.ArcadeRiseShare;
        float shaft = Math.Clamp(
            MathF.Min(height * s.ArcadeShaftShare, height - s.PlinthHeight - s.CapitalHeight - rise - s.CourseHeight - s.TopMargin), s.ShaftMin, s.ShaftMax);
        float foot = s.PlinthHeight;
        for (int tier = 0; foot + shaft + s.CapitalHeight + rise + s.CourseHeight <= height + s.FitTolerance; tier++)
        {
            bool arcade = tier == 0;
            float pilaster = arcade ? s.ArcadePilaster : s.UpperPilaster;
            float spring = foot + shaft + s.CapitalHeight;
            foreach (float x in xs)
            {
                list.Add(new WallPlacement(WallPiece.Shaft, new Vector3(x, foot, s.ShaftDepth), new Vector2(pilaster, shaft)));
                list.Add(new WallPlacement(WallPiece.Capital, new Vector3(x, foot + shaft + s.CapitalHeight * 0.5f, s.CapitalDepth),
                    new Vector2(pilaster + s.CapitalOverhang, s.CapitalHeight)));
            }

            float top = spring + rise;
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float mid = (xs[i] + xs[i + 1]) * 0.5f;
                float inner = xs[i + 1] - xs[i] - pilaster;
                if (arcade)
                {
                    BlindArch(list, s, seed, i, mid, inner, foot, spring, span, rise);
                }
                else
                {
                    Lancets(list, s, seed, tier, i, xs[i], inner, pilaster, foot, spring);
                }
            }

            list.Add(new WallPlacement(WallPiece.Course, new Vector3(0f, top + s.CourseHeight * 0.5f, s.CourseDepth), new Vector2(width, s.CourseHeight)));
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                if (Hash01(seed, FrostChance + tier, i) < frost)
                {
                    float mid = (xs[i] + xs[i + 1]) * 0.5f, inner = xs[i + 1] - xs[i] - pilaster;
                    float length = s.IcicleLength + s.IcicleLengthSpread * Hash01(seed, FrostLength + tier, i);
                    list.Add(new WallPlacement(WallPiece.Icicles, new Vector3(mid, top, s.IcicleDepth), new Vector2(inner * s.IcicleWidthShare, length)));
                }
            }

            // The tiers above: slimmer shafts, lancets under a lower arch.
            foot = top + s.CourseHeight;
            shaft = Math.Clamp(height * s.UpperShaftShare, s.UpperShaftMin, s.UpperShaftMax);
            span = MathF.Min(gap, s.UpperSpanMax);
            rise = s.UpperRise;
        }

        return list;
    }

    /// <summary>A blind arch over a shadowed bay; a rose in some tympana; now and then the arch has fallen.</summary>
    private static void BlindArch(List<WallPlacement> list, NaveWallSpec s, int seed, int bay, float mid, float inner, float foot, float spring, float span, float rise)
    {
        list.Add(new WallPlacement(WallPiece.Recess, new Vector3(mid, (foot + spring) * 0.5f, s.RecessDepth), new Vector2(inner, spring - foot)));
        if (Hash01(seed, FallenArch, bay) < s.FallenArchChance)
        {
            return;
        }

        list.Add(new WallPlacement(WallPiece.Arch, new Vector3(mid, spring, s.ArchDepth), new Vector2(span, rise)));
        float r = MathF.Min(inner * s.RoseWidthShare, rise * s.RoseRiseShare);
        if (Hash01(seed, RoseChance, bay) < s.RoseChance && r > s.RoseMinRadius)
        {
            list.Add(new WallPlacement(WallPiece.Rose, new Vector3(mid, spring + rise * s.RoseHeightShare, s.RoseDepth), new Vector2(r, r), Hash01(seed, RoseTurn, bay) * MathF.PI));
        }
    }

    /// <summary>Lancet niches, one to three to a bay, each on its own two jambs; the odd one walled up.</summary>
    private static void Lancets(List<WallPlacement> list, NaveWallSpec s, int seed, int tier, int bay, float bayStart, float inner, float pilaster, float foot, float spring)
    {
        int n = inner > s.ThreeLancetsOver ? 3 : inner > s.TwoLancetsOver ? 2 : 1;
        float lancet = MathF.Min(s.LancetMax, inner / (n * s.LancetRoom));
        float jamb = s.Jamb;
        float height = spring - foot - s.LancetInset;
        for (int k = 0; k < n; k++)
        {
            if (Hash01(seed, WalledUp + tier, bay * LancetStride + k) < s.WalledUpChance)
            {
                continue;
            }

            float lx = bayStart + pilaster * 0.5f + inner * (k + 0.5f) / n;
            list.Add(new WallPlacement(WallPiece.Recess, new Vector3(lx, (foot + spring) * 0.5f + s.LancetLift, s.RecessDepth), new Vector2(lancet, height)));
            list.Add(new WallPlacement(WallPiece.Shaft, new Vector3(lx - (lancet + jamb) * 0.5f, foot + s.LancetInset, s.LancetDepth), new Vector2(jamb, height)));
            list.Add(new WallPlacement(WallPiece.Shaft, new Vector3(lx + (lancet + jamb) * 0.5f, foot + s.LancetInset, s.LancetDepth), new Vector2(jamb, height)));
            list.Add(new WallPlacement(WallPiece.Arch, new Vector3(lx, spring, s.LancetDepth), new Vector2(lancet + jamb, (lancet + jamb) * s.LancetRiseShare)));
        }
    }
}
