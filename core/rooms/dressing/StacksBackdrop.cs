using System;
using System.Collections.Generic;
using System.Numerics;
using static TheDeep.DressingLayout;

namespace TheDeep;

/// <summary>An archive-stacks backdrop (<see cref="BackdropStyle.Stacks"/>): the rules over <see cref="StacksBackdropSpec"/>.</summary>
public static class StacksBackdrop
{
    /// <summary>The salts of its decisions; the walkway's floor is added to <see cref="Walkway"/>, so each draws its own.</summary>
    private const int MissingTower = 1, TowerTop = 2, Walkway = 3;

    private static readonly StacksBackdropSpec Spec = StacksBackdropSpec.Default;

    /// <summary>The pieces of one row of the stacks.</summary>
    public static List<BackdropPlacement> Build(BackdropRow row)
    {
        StacksBackdropSpec s = Spec;
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = BackdropLayout.Bays(row);
        var floors = new int[xs.Count];
        for (int k = 0; k < xs.Count; k++)
        {
            // Towers of stacked rack segments, of uneven height; the odd one missing.
            if (Hash01(row.Seed, MissingTower, k) < s.MissingTowerChance)
            {
                floors[k] = 0;
                continue;
            }

            float top = row.TopY - (row.TopY - row.FloorY) * s.TowerShortfallShare * Hash01(row.Seed, TowerTop, k);
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
                if (Hash01(row.Seed, Walkway + j, k) < s.NoWalkwayChance)
                {
                    continue;
                }

                float at = row.BaseY + j * d.TowerHeight;
                list.Add(new BackdropPlacement(BackdropPiece.Walkway, new Vector3(mid, at, 0f)));
                list.Add(new BackdropPlacement(BackdropPiece.Icicles, new Vector3(mid, at - d.WalkwayHeight * 0.5f, d.WalkwayDepth * s.IcicleDepthShare)));
            }
        }

        return list;
    }
}
