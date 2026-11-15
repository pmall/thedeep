using System;
using System.Collections.Generic;
using System.Numerics;
using static TheDeep.DressingLayout;

namespace TheDeep;

/// <summary>An archive's wall (<see cref="WallStyle.Archive"/>): the rules over <see cref="ArchiveWallSpec"/>.</summary>
public static class ArchiveWall
{
    /// <summary>The salts of its decisions; the shelf's number is added to each, so every shelf draws its own.</summary>
    private const int FallenShelf = 1, IcicleChance = 20, IcicleLength = 40, Looted = 100, RackLook = 200;

    /// <summary>The salt's stride along a row of racks (the index of a rack's roll is the bay times this plus the rack's).</summary>
    private const int RackStride = 37;

    private static readonly ArchiveWallSpec Spec = ArchiveWallSpec.Default;

    /// <summary>The archive wall's pieces for a wall <paramref name="width"/> by <paramref name="height"/> meters.</summary>
    public static List<WallPlacement> Build(float width, float height, int seed)
    {
        ArchiveWallSpec s = Spec;
        var list = new List<WallPlacement>();
        List<float> xs = WallLayout.Pilasters(width);
        foreach (float x in xs)
        {
            list.Add(new WallPlacement(WallPiece.Pier, new Vector3(x, 0f, s.PierDepth), new Vector2(s.PierWidth, height)));
        }

        int shelves = Math.Max(1, (int)((height - s.TopMargin) / s.ShelfPitch));
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            float a = xs[i] + s.BayMargin, b = xs[i + 1] - s.BayMargin;
            float span = b - a, mid = (a + b) * 0.5f;
            int racks = Math.Max(1, (int)(span / s.RackPitch));
            float pitch = span / racks;
            for (int j = 0; j < shelves; j++)
            {
                // A fallen shelf now and then leaves a gap of bare wall.
                if (j > 0 && Hash01(seed, FallenShelf + j, i) < s.FallenShelfChance)
                {
                    continue;
                }

                float y = j * s.ShelfPitch;
                if (j > 0)
                {
                    list.Add(new WallPlacement(WallPiece.Shelf, new Vector3(mid, y, s.ShelfDepth), new Vector2(span + s.ShelfOverhang, s.ShelfThickness)));
                    if (Hash01(seed, IcicleChance + j, i) < s.IcicleChance)
                    {
                        float length = s.IcicleLength + s.IcicleLengthSpread * Hash01(seed, IcicleLength + j, i);
                        list.Add(new WallPlacement(WallPiece.Icicles, new Vector3(mid, y - s.ShelfThickness * 0.5f, s.IcicleDepth), new Vector2(span * s.IcicleWidthShare, length)));
                    }
                }

                for (int k = 0; k < racks; k++)
                {
                    if (Hash01(seed, Looted + j, i * RackStride + k) < s.LootedChance)
                    {
                        continue; // looted: a gap in the row
                    }

                    float lift = j > 0 ? s.ShelfThickness * 0.5f : 0f;
                    list.Add(new WallPlacement(WallPiece.Rack, new Vector3(a + (k + 0.5f) * pitch, y + lift, s.RackDepth),
                        new Vector2(pitch * s.RackWidthShare, s.ShelfPitch - s.RackHeightMargin),
                        Variant: (int)(Hash01(seed, RackLook + j, i * RackStride + k) * s.RackVariants)));
                }
            }
        }

        return list;
    }
}
