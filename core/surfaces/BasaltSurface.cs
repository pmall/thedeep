using System;

namespace TheDeep;

/// <summary>
/// The face of a basalt column, as a tileable pattern (<see cref="ISurfacePattern"/>) for the rock material's columnar jointing
/// (<c>assets/shaders/rock.gdshader</c>'s <c>columnar</c>): dense, fine-grained rock with no beds, broken into shallow
/// conchoidal facets each tilted its own way, pocked with gas vesicles of two sizes, crossed here and there by a hairline crack
/// that shows in stretches. The joints between columns and across them are the material's, at world scale, so they never repeat
/// with the tile; each piece of column takes its own part of the tile. The knobs are its properties (meters unless named
/// otherwise).
/// </summary>
public sealed record BasaltSurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 4.0;

    /// <summary>Conchoidal facets per tile side: each tilts its own way by up to <see cref="FacetSlope"/>.</summary>
    public int FacetCells { get; init; } = 11;

    /// <summary>How far a facet leans out of the face, at most (meters per meter).</summary>
    public double FacetSlope { get; init; } = 0.035;

    /// <summary>How far a facet's tone departs from the rock's, either way, a share of it.</summary>
    public double FacetContrast { get; init; } = 0.06;

    /// <summary>The fine grain: its relief and its first octave's cells per tile.</summary>
    public double GrainMeters { get; init; } = 0.006;

    /// <summary>The grain noise's first octave's cells per tile.</summary>
    public int GrainCells { get; init; } = 32;

    /// <summary>How much the grain shows in the tone.</summary>
    public double GrainTone { get; init; } = 0.18;

    /// <summary>Small vesicles: cells per tile (one per cell at most), the share of cells holding one, and their depth.</summary>
    public int VesicleCells { get; init; } = 110;

    /// <summary>Share of the small vesicles' cells (0 to 1) that hold one.</summary>
    public double VesicleShare { get; init; } = 0.22;

    /// <summary>A small vesicle's depth, meters.</summary>
    public double VesicleMeters { get; init; } = 0.008;

    /// <summary>Large vesicles: cells per tile, the share holding one, and their depth.</summary>
    public int LargeVesicleCells { get; init; } = 36;

    /// <summary>Share of the large vesicles' cells (0 to 1) that hold one.</summary>
    public double LargeVesicleShare { get; init; } = 0.07;

    /// <summary>A large vesicle's depth, meters.</summary>
    public double LargeVesicleMeters { get; init; } = 0.014;

    /// <summary>Hairline cracks: cells per tile of their web, the share of their length that shows, their half width and depth.</summary>
    public int CrackCells { get; init; } = 6;

    /// <summary>Share of the cracks' length that shows (0 to 1).</summary>
    public double CrackShare { get; init; } = 0.3;

    /// <summary>A hairline crack's half width, meters.</summary>
    public double CrackMeters { get; init; } = 0.012;

    /// <summary>A hairline crack's depth, meters.</summary>
    public double CrackDepthMeters { get; init; } = 0.012;

    /// <summary>The rock's roughness, and how far a facet's departs from it.</summary>
    public double Roughness { get; init; } = 0.82;

    /// <summary>How far a facet's roughness departs from the rock's, either way.</summary>
    public double RoughnessSpread { get; init; } = 0.05;

    /// <summary>The seed of every noise of the pattern: another seed gives another face of the same character.</summary>
    public int Seed { get; init; } = 31;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double tile = TileMeters;
        double u = TileNoise.Wrap(alongMeters, tile) / tile;
        double v = TileNoise.Wrap(upMeters, tile) / tile;

        // Facets: a Voronoi web, each cell leaning its own way from its site, its rims soft (a conchoidal break, no fissure).
        VoronoiCell facet = TileNoise.Voronoi(u * FacetCells, v * FacetCells, FacetCells, Seed + 1);
        double cellMeters = tile / FacetCells;
        double tiltX = (TileNoise.Hash(facet.Cell, 0, Seed + 2) - 0.5) * 2.0 * FacetSlope;
        double tiltY = (TileNoise.Hash(facet.Cell, 1, Seed + 2) - 0.5) * 2.0 * FacetSlope;
        double tilt = -((facet.ToSiteX * tiltX) + (facet.ToSiteY * tiltY)) * cellMeters
            * TileNoise.SmoothStep(0.0, 0.05, facet.Border * cellMeters);
        double facetTone = (TileNoise.Hash(facet.Cell, 2, Seed + 2) - 0.5) * 2.0 * FacetContrast;
        double rim = TileNoise.Line(facet.Border * cellMeters, 0.02);

        double grain = TileNoise.Fractal(u * GrainCells, v * GrainCells, GrainCells, GrainCells, 4, Seed + 3) - 0.5;
        double small = TileNoise.Pit(u, v, VesicleCells, VesicleShare, Seed + 4);
        double large = TileNoise.Pit(u, v, LargeVesicleCells, LargeVesicleShare, Seed + 5);

        // Hairline cracks, in stretches.
        VoronoiCell web = TileNoise.Voronoi(u * CrackCells, v * CrackCells, CrackCells, Seed + 6);
        double showsNoise = TileNoise.Fractal(u * 8, v * 8, 8, 8, 2, Seed + 7);
        double threshold = 1.0 - CrackShare;
        double shows = TileNoise.SmoothStep(threshold - 0.05, threshold + 0.05, showsNoise);
        double crack = TileNoise.Line(web.Border * tile / CrackCells, CrackMeters) * shows;

        double height = tilt + (grain * 2.0 * GrainMeters) - (small * VesicleMeters) - (large * LargeVesicleMeters)
            - (crack * CrackDepthMeters) - (rim * 0.002);
        double tone = 1.0 + facetTone + (grain * GrainTone) - (small * 0.12) - (large * 0.25) - (crack * 0.3);
        double cavity = Math.Max(Math.Max(small * 0.5, large * 0.8), crack * 0.8);
        double rough = Roughness + ((TileNoise.Hash(facet.Cell, 3, Seed + 2) - 0.5) * 2.0 * RoughnessSpread) + (cavity * 0.08);
        return new SurfacePoint(height, tone, cavity, Math.Clamp(rough, 0.0, 1.0));
    }
}
