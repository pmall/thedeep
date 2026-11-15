using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// Pure, deterministic dressings for a room's back wall (<see cref="WallDressing"/> builds the meshes): bays between
/// pilasters, each filled with one motif, so a wide plain wall reads as a built thing at the scale of the room. Each style
/// is rules over its spec (<see cref="FoundryWallSpec"/>, <see cref="MachineryWallSpec"/>, <see cref="ArchiveWallSpec"/>,
/// <see cref="NaveWallSpec"/>); this class picks the style and holds what they share.
/// </summary>
public static class WallLayout
{
    /// <summary>Pilasters stand about this far apart, meters (the bays share the width evenly).</summary>
    public const float Bay = 7f;

    /// <summary>
    /// The wall's pieces. <paramref name="frost"/> (0 to 1) is the share of a nave's bays with icicles under their courses;
    /// the other styles ignore it.
    /// </summary>
    public static List<WallPlacement> Build(WallStyle style, float width, float height, int seed, float frost = 0f) => style switch
    {
        WallStyle.Machinery => MachineryWall.Build(width, height, seed),
        WallStyle.Archive => ArchiveWall.Build(width, height, seed),
        WallStyle.Nave => NaveWall.Build(width, height, seed, frost),
        _ => FoundryWall.Build(width, height, seed),
    };

    /// <summary>The X of each pilaster, evenly spaced, the first and last at the wall's ends.</summary>
    public static List<float> Pilasters(float width)
    {
        int bays = Math.Max(1, (int)MathF.Round(width / Bay));
        var xs = new List<float>();
        for (int i = 0; i <= bays; i++)
        {
            xs.Add(-width * 0.5f + width * i / bays);
        }

        return xs;
    }
}
