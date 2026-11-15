using System;
using System.Collections.Generic;
using System.Numerics;
using static TheDeep.DressingLayout;

namespace TheDeep;

/// <summary>A nave backdrop (<see cref="BackdropStyle.Nave"/>): the rules over <see cref="NaveBackdropSpec"/>.</summary>
public static class NaveBackdrop
{
    /// <summary>The salts of its decisions; the arch's tier is added to <see cref="FallenArch"/>, so each draws its own.</summary>
    private const int BrokenPier = 1, BrokenHeight = 2, FallenArch = 3, PierShaft = 40;

    private static readonly NaveBackdropSpec Spec = NaveBackdropSpec.Default;

    /// <summary>The pieces of one row of the nave.</summary>
    public static List<BackdropPlacement> Build(BackdropRow row)
    {
        NaveBackdropSpec s = Spec;
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = BackdropLayout.Bays(row);

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
            height[k] = Hash01(row.Seed, BrokenPier, k) < s.BrokenPierChance && tiers.Count > 1 ? 1 + (int)(Hash01(row.Seed, BrokenHeight, k) * (tiers.Count - 1)) : tiers.Count;
        }

        for (int k = 0; k < xs.Count; k++)
        {
            for (int j = 0; j < height[k]; j++)
            {
                list.Add(new BackdropPlacement(j == 0 ? BackdropPiece.Pier : BackdropPiece.UpperPier, new Vector3(xs[k], tiers[j], 0f)));
            }

            if (height[k] > 1 && Hash01(row.Seed, PierShaft, k) < s.ShaftChance)
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
                if (Hash01(row.Seed, FallenArch + j, k) < s.FallenArchChance)
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
}
