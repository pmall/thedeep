using System;

namespace TheDeep;

/// <summary>
/// The coat of rime in a frozen layer, as a tileable pattern (<see cref="ISurfacePattern"/>) that every material that frosts
/// lays over its own surface (<c>assets/shaders/lib/frost.gdshaderinc</c>): a soft, mottled coat, paler and darker in blotches
/// a few tens of centimeters across, broken by small soft oval gaps where it is thin; frosted crusts standing a little proud
/// of it; patches of hoarfrost whose fronds feather out in streaks; and tiny crystals, each a smooth facet tilted its own way,
/// that catch the light at their own angle.
/// <para>
/// Its <see cref="SurfacePoint.Tone"/> multiplies the rime's color. Its cavity is the coat's thickness (0 to 1), which the
/// material compares with how much rime the place takes, so where it takes little only the thickest parts show (crusts,
/// fronds) and the gaps show the surface under it last. Its roughness is the rime's, low on the crystals: through the specular
/// they glint as the light and the view move, steadily, and their mipmaps blend them away rather than let them sparkle at a
/// distance (<c>docs/art-direction.md#stable-image</c>). It has no cells. The knobs are its properties (meters unless named
/// otherwise).
/// </para>
/// </summary>
public sealed record FrostSurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 4.0;

    /// <summary>The coat's mottle: its first octave's cells per tile, how far it moves the tone, and how far the thickness.</summary>
    public int MottleCells { get; init; } = 7;

    public double Mottle { get; init; } = 0.16;

    public double MottleThickness { get; init; } = 0.55;

    /// <summary>The gaps: cells per tile side (one gap per cell at most), the share of cells with one where the coat is thin,
    /// their half length at least and at most and their half width (shares of a cell), and how dark the coat is at their rim.</summary>
    public int GapCells { get; init; } = 16;

    public double GapShare { get; init; } = 0.7;

    public double GapMinLength { get; init; } = 0.3;

    public double GapMaxLength { get; init; } = 0.55;

    public double GapWidth { get; init; } = 0.3;

    public double GapRimTone { get; init; } = 0.75;

    /// <summary>How much thinner the coat is at a gap's middle.</summary>
    public double GapDepth { get; init; } = 0.7;

    /// <summary>The crusts: their noise's first octave's cells per tile, the noise level where a crust starts and where it is
    /// full, how far it stands proud, and how pale it is.</summary>
    public int CrustCells { get; init; } = 11;

    public double CrustFrom { get; init; } = 0.56;

    public double CrustTo { get; init; } = 0.7;

    public double CrustMeters { get; init; } = 0.003;

    public double CrustTone { get; init; } = 0.12;

    /// <summary>The hoarfrost: the cells per tile of the patches it grows in, its fronds' cells per tile along and across them
    /// (on the diagonals, long and narrow), how pale a frond is and how far it stands proud.</summary>
    public int HoarCells { get; init; } = 5;

    public int FrondCellsAlong { get; init; } = 10;

    public int FrondCellsAcross { get; init; } = 70;

    public double FrondTone { get; init; } = 0.06;

    public double FrondMeters { get; init; } = 0.0012;

    /// <summary>The crystals: cells per tile side (one crystal per cell at most), the share of cells with one where the coat is
    /// thick, their radius (a share of a cell), how far a facet tilts (a slope), how pale and how smooth it is.</summary>
    public int CrystalCells { get; init; } = 120;

    public double CrystalShare { get; init; } = 0.22;

    public double CrystalRadius { get; init; } = 0.4;

    public double CrystalTilt { get; init; } = 0.6;

    public double CrystalTone { get; init; } = 0.05;

    public double CrystalRoughness { get; init; } = 0.12;

    /// <summary>The rime's roughness, and the crusts'.</summary>
    public double Roughness { get; init; } = 0.66;

    public double CrustRoughness { get; init; } = 0.8;

    public int Seed { get; init; } = 53;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double tile = TileMeters;
        double u = TileNoise.Wrap(alongMeters, tile) / tile;
        double v = TileNoise.Wrap(upMeters, tile) / tile;

        // The mottle: the coat's tone and thickness in blotches, one noise for both, so the thick parts are the pale ones.
        double mottle = TileNoise.Fractal(u * MottleCells, v * MottleCells, MottleCells, MottleCells, 4, Seed + 1);
        double thick = 0.5 + ((mottle - 0.5) * 2.0 * MottleThickness);
        double tone = 1.0 + ((mottle - 0.5) * 2.0 * Mottle);

        // Crusts, frosted and proud.
        double crust = TileNoise.SmoothStep(CrustFrom, CrustTo, TileNoise.Fractal(u * CrustCells, v * CrustCells, CrustCells, CrustCells, 3, Seed + 2));

        // Hoarfrost fronds on the two diagonals, one way or the other in patches.
        double hoar = TileNoise.SmoothStep(0.5, 0.68, TileNoise.Fractal(u * HoarCells, v * HoarCells, HoarCells, HoarCells, 2, Seed + 3));
        double way = TileNoise.SmoothStep(0.4, 0.6, TileNoise.Value(u * HoarCells, v * HoarCells, HoarCells, HoarCells, Seed + 4));
        double frond = TileNoise.Lerp(Frond(u + v, v - u, Seed + 5), Frond(u - v, u + v, Seed + 6), way) * hoar;

        double gap = Gap(u, v, mottle);
        thick = Math.Clamp(thick + (0.3 * crust) + (0.15 * frond) - (GapDepth * gap), 0.0, 1.0);
        tone *= 1.0 + (CrustTone * crust) + (FrondTone * frond);
        tone *= TileNoise.Lerp(1.0, GapRimTone, Math.Min(1.0, gap * 1.5));
        double height = (crust * CrustMeters) + (frond * FrondMeters) - (gap * CrustMeters);
        double rough = TileNoise.Lerp(Roughness, CrustRoughness, crust);

        // Crystals stand on the thick coat, each a smooth facet tilted its own way.
        (double facet, double crystal) = Crystal(u, v, thick);
        height += facet;
        tone *= 1.0 + (CrystalTone * crystal);
        rough = TileNoise.Lerp(rough, CrystalRoughness, crystal);
        return new SurfacePoint(height, tone, thick, rough);
    }

    /// <summary>A frond's paleness (0 to 1) at a point given on a diagonal lattice: <paramref name="along"/> and
    /// <paramref name="across"/> are sums and differences of the tile's coordinates, so whole periods keep it tiling.</summary>
    private double Frond(double along, double across, int seed)
    {
        double streak = TileNoise.Fractal(along * FrondCellsAlong, across * FrondCellsAcross, FrondCellsAlong, FrondCellsAcross, 3, seed);
        return TileNoise.SmoothStep(0.52, 0.72, streak);
    }

    /// <summary>How deep in a gap the point is (0 to 1): a soft oval, each at its own angle, from each cell around the point
    /// that holds one; more of them where the coat is thin (<paramref name="mottle"/> low).</summary>
    private double Gap(double u, double v, double mottle)
    {
        double x = u * GapCells;
        double y = v * GapCells;
        int cx = (int)Math.Floor(x);
        int cy = (int)Math.Floor(y);
        double share = GapShare * TileNoise.Lerp(0.4, 1.0, TileNoise.SmoothStep(0.7, 0.3, mottle));
        double best = 0.0;
        for (int j = -1; j <= 1; j++)
        {
            for (int i = -1; i <= 1; i++)
            {
                int wx = TileNoise.Wrap(cx + i, GapCells);
                int wy = TileNoise.Wrap(cy + j, GapCells);
                if (TileNoise.Hash(wx, wy, Seed + 7) >= share)
                {
                    continue;
                }

                double mx = cx + i + 0.2 + (0.6 * TileNoise.Hash(wx, wy, Seed + 8));
                double my = cy + j + 0.2 + (0.6 * TileNoise.Hash(wx, wy, Seed + 9));
                double angle = TileNoise.Hash(wx, wy, Seed + 10) * Math.PI;
                double half = TileNoise.Lerp(GapMinLength, GapMaxLength, TileNoise.Hash(wx, wy, Seed + 11));
                double width = GapWidth * (0.7 + (0.6 * TileNoise.Hash(wx, wy, Seed + 12)));
                double dx = Math.Cos(angle);
                double dy = Math.Sin(angle);
                double s = (((x - mx) * dx) + ((y - my) * dy)) / half;
                double t = (((x - mx) * -dy) + ((y - my) * dx)) / width;

                // An ellipse, its border soft.
                double lens = 1.0 - (s * s);
                if (lens <= 0.0)
                {
                    continue;
                }

                double r = Math.Abs(t) / Math.Sqrt(lens);
                best = Math.Max(best, 1.0 - TileNoise.SmoothStep(0.1, 1.0, r));
            }
        }

        return best;
    }

    /// <summary>The crystal at a point: its facet's height (meters, a tilted plane faded to nothing at its rim) and how much of
    /// the point it covers (0 to 1). One per cell at most, where the coat is thick.</summary>
    private (double Facet, double Cover) Crystal(double u, double v, double thick)
    {
        double x = u * CrystalCells;
        double y = v * CrystalCells;
        int cx = (int)Math.Floor(x);
        int cy = (int)Math.Floor(y);
        int wx = TileNoise.Wrap(cx, CrystalCells);
        int wy = TileNoise.Wrap(cy, CrystalCells);
        if (TileNoise.Hash(wx, wy, Seed + 13) >= CrystalShare * TileNoise.SmoothStep(0.45, 0.75, thick))
        {
            return (0.0, 0.0);
        }

        double radius = CrystalRadius * (0.7 + (0.3 * TileNoise.Hash(wx, wy, Seed + 14)));
        double px = cx + 0.5 + ((TileNoise.Hash(wx, wy, Seed + 15) - 0.5) * (1.0 - (2.0 * radius)));
        double py = cy + 0.5 + ((TileNoise.Hash(wx, wy, Seed + 16) - 0.5) * (1.0 - (2.0 * radius)));
        double r = Math.Sqrt(((x - px) * (x - px)) + ((y - py) * (y - py))) / radius;
        double cover = 1.0 - TileNoise.SmoothStep(0.6, 1.0, r);
        if (cover <= 0.0)
        {
            return (0.0, 0.0);
        }

        // The facet's tilt, any way round, as a slope over the cell's meters.
        double angle = TileNoise.Hash(wx, wy, Seed + 17) * 2.0 * Math.PI;
        double cellMeters = TileMeters / CrystalCells;
        double lean = CrystalTilt * (0.5 + (0.5 * TileNoise.Hash(wx, wy, Seed + 18)));
        double plane = (((x - px) * Math.Cos(angle)) + ((y - py) * Math.Sin(angle))) * cellMeters * lean;
        return (plane * cover, cover);
    }
}
