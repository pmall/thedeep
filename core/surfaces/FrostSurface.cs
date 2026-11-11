using System;

namespace TheDeep;

/// <summary>
/// The coat of rime in a frozen layer, as a tileable pattern (<see cref="ISurfacePattern"/>) that every material that frosts
/// lays over its own surface (<c>assets/shaders/lib/frost.gdshaderinc</c>): a soft, mottled coat, paler and darker in blotches
/// a few tens of centimeters across, broken by small soft gaps where it is thin, each a bent, uneven oval; frosted crusts standing a little proud
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

    /// <summary>How far the mottle moves the rime's tone, a share either way (0.16: from 16% darker to 16% paler).</summary>
    public double Mottle { get; init; } = 0.16;

    /// <summary>How far the mottle moves the coat's thickness either side of half (thickness runs 0 to 1).</summary>
    public double MottleThickness { get; init; } = 0.55;

    /// <summary>The gaps: cells per tile side (one gap per cell at most), the share of cells with one where the coat is thin,
    /// their half length at least and at most and their half width (shares of a cell), and how dark the coat is at their rim.</summary>
    public int GapCells { get; init; } = 12;

    /// <summary>Share of cells (0 to 1) that hold a gap where the coat is thinnest; four tenths of it where the coat is
    /// thick.</summary>
    public double GapShare { get; init; } = 0.6;

    /// <summary>A gap's half length at least, a share of a cell.</summary>
    public double GapMinLength { get; init; } = 0.3;

    /// <summary>A gap's half length at most, a share of a cell.</summary>
    public double GapMaxLength { get; init; } = 0.6;

    /// <summary>A gap's half width, a share of a cell; each gap takes between 0.5 and 1.3 times it.</summary>
    public double GapWidth { get; init; } = 0.26;

    /// <summary>What the rime's tone is multiplied by in a gap (below 1 darkens), reached two thirds of the way from its rim to
    /// its middle.</summary>
    public double GapRimTone { get; init; } = 0.8;

    /// <summary>How much thinner the coat is at a gap's middle.</summary>
    public double GapDepth { get; init; } = 0.75;

    /// <summary>How far the gaps' outlines are pushed about (a share of a cell), by a noise of <see cref="GapWarpCells"/> per
    /// tile, so they bend and swell unevenly instead of reading as a field of regular ovals.</summary>
    public double GapWarp { get; init; } = 0.45;

    /// <summary>Cells per tile of the noise that pushes the gaps' outlines about.</summary>
    public int GapWarpCells { get; init; } = 10;

    /// <summary>The crusts: their noise's first octave's cells per tile, the noise level where a crust starts and where it is
    /// full, how far it stands proud, and how pale it is.</summary>
    public int CrustCells { get; init; } = 11;

    /// <summary>The crust noise's level (0 to 1) where a crust starts.</summary>
    public double CrustFrom { get; init; } = 0.56;

    /// <summary>The crust noise's level (0 to 1) where a crust is full.</summary>
    public double CrustTo { get; init; } = 0.7;

    /// <summary>How far a full crust stands proud of the coat, meters; a gap's middle sinks as far below it.</summary>
    public double CrustMeters { get; init; } = 0.003;

    /// <summary>How much paler a full crust is, a share of the rime's tone.</summary>
    public double CrustTone { get; init; } = 0.12;

    /// <summary>The hoarfrost: the cells per tile of the patches it grows in, its fronds' cells per tile along and across them
    /// (on the diagonals, long and narrow), how pale a frond is and how far it stands proud.</summary>
    public int HoarCells { get; init; } = 5;

    /// <summary>The frond noise's cells per tile along the fronds (few: they run long).</summary>
    public int FrondCellsAlong { get; init; } = 10;

    /// <summary>The frond noise's cells per tile across the fronds (many: they are narrow).</summary>
    public int FrondCellsAcross { get; init; } = 70;

    /// <summary>How much paler a frond is, a share of the rime's tone.</summary>
    public double FrondTone { get; init; } = 0.06;

    /// <summary>How far a frond stands proud of the coat, meters.</summary>
    public double FrondMeters { get; init; } = 0.0012;

    /// <summary>The crystals: cells per tile side (one crystal per cell at most), the share of cells with one where the coat is
    /// thick, their radius (a share of a cell), how far a facet tilts (a slope), how pale and how smooth it is.</summary>
    public int CrystalCells { get; init; } = 120;

    /// <summary>Share of cells (0 to 1) that hold a crystal where the coat is thickest; none where it is thin.</summary>
    public double CrystalShare { get; init; } = 0.22;

    /// <summary>A crystal's radius at most, a share of a cell; each takes between 0.7 and 1 times it.</summary>
    public double CrystalRadius { get; init; } = 0.4;

    /// <summary>A facet's steepest tilt, as a slope (rise over run); each crystal takes between half of it and all of
    /// it.</summary>
    public double CrystalTilt { get; init; } = 0.6;

    /// <summary>How much paler a crystal is, a share of the rime's tone.</summary>
    public double CrystalTone { get; init; } = 0.05;

    /// <summary>The roughness (0 to 1) of a crystal's facet, low so it glints.</summary>
    public double CrystalRoughness { get; init; } = 0.12;

    /// <summary>The rime's roughness, and the crusts'.</summary>
    public double Roughness { get; init; } = 0.66;

    /// <summary>The roughness (0 to 1) of a full crust.</summary>
    public double CrustRoughness { get; init; } = 0.8;

    /// <summary>The seed of every noise of the pattern: another seed gives another coat of the same character.</summary>
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

    /// <summary>How deep in a gap the point is (0 to 1): a soft oval, each at its own angle, its outline bent by a noise, from
    /// each cell around the point that holds one; more of them where the coat is thin (<paramref name="mottle"/> low).</summary>
    private double Gap(double u, double v, double mottle)
    {
        double x = (u * GapCells) + ((TileNoise.Fractal(u * GapWarpCells, v * GapWarpCells, GapWarpCells, GapWarpCells, 2, Seed + 19) - 0.5) * 2.0 * GapWarp);
        double y = (v * GapCells) + ((TileNoise.Fractal(u * GapWarpCells, v * GapWarpCells, GapWarpCells, GapWarpCells, 2, Seed + 20) - 0.5) * 2.0 * GapWarp);
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
                double width = GapWidth * (0.5 + (0.8 * TileNoise.Hash(wx, wy, Seed + 12)));
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
