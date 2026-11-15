using System;
using System.Collections.Generic;
using System.Numerics;
using static TheDeep.DressingLayout;

namespace TheDeep;

/// <summary>The deep works' wall (<see cref="WallStyle.Machinery"/>): the rules over <see cref="MachineryWallSpec"/>.</summary>
public static class MachineryWall
{
    /// <summary>The salts of its decisions per bay or piston; the stacked banks add their number to the last five, so each draws its own.</summary>
    private const int PistonShort = 7, PistonHeight = 8, BayHasGears = 2, GreatSize = 3, GearHeight = 4, GearSpin = 5, LowBank = 6, LowBankWidth = 9;

    private const int StackHeightRoll = 10, StackWidthRoll = 20, StackPresent = 30, ChestSlit = 40, StackGapRoll = 50;

    private const int SmallSize = 11, Lean = 12, LeanSide = 13, SmallSide = 14;

    private static readonly MachineryWallSpec Spec = MachineryWallSpec.Default;

    private static readonly float[] UpFirst = [1f, -1f];

    private static readonly float[] DownFirst = [-1f, 1f];

    /// <summary>The height of the wind chest under a bank <paramref name="height"/> meters tall, meters.</summary>
    public static float BankChest(float height) => Math.Clamp(height * Spec.ChestShare, Spec.ChestMin, Spec.ChestMax);

    /// <summary>How many teeth a gear of <paramref name="radius"/> meters has.</summary>
    public static int GearTeeth(float radius) => Math.Max(Spec.MinTeeth, (int)MathF.Round(MathF.Tau * Spec.GearMesh * radius / Spec.ToothPitch));

    /// <summary>The machinery wall's pieces for a wall <paramref name="width"/> by <paramref name="height"/> meters.</summary>
    public static List<WallPlacement> Build(float width, float height, int seed)
    {
        MachineryWallSpec s = Spec;
        var list = new List<WallPlacement>();
        List<float> xs = WallLayout.Pilasters(width);
        for (int k = 0; k < xs.Count; k++)
        {
            // Pistons on the bay lines, some cut short (the machine stopped mid-stroke).
            float tall = Hash01(seed, PistonShort, k) < s.ShortPistonChance ? height * (s.ShortPistonShare + s.ShortPistonSpread * Hash01(seed, PistonHeight, k)) : height;
            list.Add(new WallPlacement(WallPiece.Piston, new Vector3(xs[k], 0f, s.PistonDepth), new Vector2(s.PistonWidth, tall)));
        }

        // A band across the wall, two on a tall one: the frame the machinery hangs in.
        float band = MathF.Min(s.BandMax, height * s.BandShare);
        list.Add(new WallPlacement(WallPiece.Girder, new Vector3(0f, band, s.BandDepth), new Vector2(width, s.BandThickness)));
        if (height > s.TallWall)
        {
            list.Add(new WallPlacement(WallPiece.Girder, new Vector3(0f, height * s.HighBandShare, s.BandDepth), new Vector2(width, s.BandThickness)));
        }

        for (int i = 0; i + 1 < xs.Count; i++)
        {
            float a = xs[i] + s.BayMargin, b = xs[i + 1] - s.BayMargin;
            float mid = (a + b) * 0.5f, span = b - a;
            if (Hash01(seed, BayHasGears, i) < s.GearBayChance)
            {
                GearTrain(list, s, seed, i, mid, span, band, height);
                UnderTheBand(list, s, seed, i, mid, span, band);
            }
            else
            {
                StackedBanks(list, s, seed, i, mid, span, height);
            }
        }

        return list;
    }

    /// <summary>Under the band, a low bank now and then, and otherwise a slit of light.</summary>
    private static void UnderTheBand(List<WallPlacement> list, MachineryWallSpec s, int seed, int bay, float mid, float span, float band)
    {
        if (Hash01(seed, LowBank, bay) < s.LowBankChance && band > s.LowBankMinBand)
        {
            float w = span * (s.LowBankWidthShare + s.LowBankWidthSpread * Hash01(seed, LowBankWidth, bay));
            list.Add(new WallPlacement(WallPiece.Bank, new Vector3(mid, s.BankFoot, s.BankBack), new Vector2(w, band - s.LowBankHeadroom)));
        }
        else
        {
            list.Add(new WallPlacement(WallPiece.Slit, new Vector3(mid, band - s.SlitBelowBand, s.SlitDepth), new Vector2(span * s.SlitSpanShare, s.SlitThickness)));
        }
    }

    /// <summary>Banks of uneven height stacked up the bay, gaps of bare wall between; a slit of light along some wind chests.</summary>
    private static void StackedBanks(List<WallPlacement> list, MachineryWallSpec s, int seed, int bay, float mid, float span, float height)
    {
        float y = s.BankFoot;
        int n = 0;
        while (y < height - s.StackTopMargin && n < s.MaxBanks)
        {
            float h = s.StackHeight + s.StackHeightSpread * Hash01(seed, StackHeightRoll + n, bay);
            h = MathF.Min(h, height - s.StackHeadroom - y);
            float w = span * (s.StackWidthShare + s.StackWidthSpread * Hash01(seed, StackWidthRoll + n, bay));
            if (Hash01(seed, StackPresent + n, bay) > s.StackGapBelow && h > s.StackMinHeight)
            {
                list.Add(new WallPlacement(WallPiece.Bank, new Vector3(mid, y, s.BankBack), new Vector2(w, h)));
                if (Hash01(seed, ChestSlit + n, bay) < s.ChestSlitChance)
                {
                    list.Add(new WallPlacement(WallPiece.Slit, new Vector3(mid, y + BankChest(h) * 0.5f, s.BankBack + s.BankDepth + s.ChestSlitInset),
                        new Vector2(w * s.ChestSlitWidthShare, s.ChestSlitThickness)));
                }
            }

            y += h + s.StackGap + s.StackGapSpread * Hash01(seed, StackGapRoll + n, bay);
            n++;
        }
    }

    /// <summary>
    /// A great gear over the band, its axle's end at its middle, and a smaller one meshed with it above or below, turned so
    /// their teeth interleave; the smaller is left out where neither side has room in the bay.
    /// </summary>
    private static void GearTrain(List<WallPlacement> list, MachineryWallSpec s, int seed, int bay, float mid, float span, float band, float height)
    {
        float r = MathF.Min(span * s.GreatSpanShare, (height - band) * s.GreatHeightShare) * (s.GreatSizeShare + s.GreatSizeSpread * Hash01(seed, GreatSize, bay));
        float y = band + s.GearLift + r + (height - band - s.GearRange - 2f * r) * Hash01(seed, GearHeight, bay);
        float rp = r * (s.SmallShare + s.SmallShareSpread * Hash01(seed, SmallSize, bay));
        float d = s.GearMesh * (r + rp);
        float lean = (s.LeanShare + s.LeanSpread * Hash01(seed, Lean, bay)) * MathF.PI * (Hash01(seed, LeanSide, bay) < 0.5f ? -1f : 1f);
        float? toward = null;
        foreach (float up in Hash01(seed, SmallSide, bay) < 0.5f ? UpFirst : DownFirst)
        {
            float angle = up * MathF.PI * 0.5f + lean;
            var c = new Vector2(mid + d * MathF.Cos(angle), y + d * MathF.Sin(angle));
            if (MathF.Abs(c.X - mid) + rp <= span * 0.5f + s.SideTolerance && c.Y - rp >= band + s.SmallBandClear && c.Y + rp <= height - s.SmallTopClear)
            {
                toward = angle;
                break;
            }
        }

        // A tooth of the great gear on the line between the middles, a gap of the small one facing it.
        float step = MathF.Tau / GearTeeth(r);
        float theta = toward ?? Hash01(seed, GearSpin, bay) * MathF.Tau;
        list.Add(new WallPlacement(WallPiece.Gear, new Vector3(mid, y, s.GearDepth), new Vector2(r, r), theta - s.GreatToothPhase * step));
        list.Add(new WallPlacement(WallPiece.Hub, new Vector3(mid, y, s.GearDepth), new Vector2(r * s.GreatHubShare, r * s.GreatHubShare)));
        if (toward is float angle2)
        {
            float pstep = MathF.Tau / GearTeeth(rp);
            var at = new Vector3(mid + d * MathF.Cos(angle2), y + d * MathF.Sin(angle2), s.SmallGearDepth);
            list.Add(new WallPlacement(WallPiece.Gear, at, new Vector2(rp, rp), angle2 + MathF.PI - s.SmallToothPhase * pstep));
            list.Add(new WallPlacement(WallPiece.Hub, at, new Vector2(rp * s.SmallHubShare, rp * s.SmallHubShare)));
        }
    }
}
