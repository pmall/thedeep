using System;

namespace TheDeep;

/// <summary>
/// The face of a basalt column, as a tileable pattern (<see cref="ISurfacePattern"/>) for the rock material's columnar jointing
/// (<c>assets/shaders/rock.gdshader</c>'s <c>columnar</c>): dense, fine-grained rock with no beds, its face gently undulating,
/// crossed by faint wavering bands (the chisel marks a column's face keeps from cracking a step at a time as it cooled), mottled
/// in broad patches, pocked with gas vesicles that gather in a few places and leave the rest of the face sound, and split here and
/// there by a short hairline crack. Nothing in it falls in rows: the face reads as one stone at any distance. The joints between
/// columns and across them, the facets and the columns' ends are the material's, at world scale, so they never repeat with the
/// tile; each piece of column takes its own part of the tile. The knobs are its properties (meters unless named otherwise).
/// </summary>
public sealed record BasaltSurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 4.0;

    /// <summary>The face's broad undulation: its first octave's cells per tile.</summary>
    public int UndulationCells { get; init; } = 5;

    /// <summary>How far the undulation rises and sinks, meters.</summary>
    public double UndulationMeters { get; init; } = 0.025;

    /// <summary>Chisel marks: how many bands per tile up the face.</summary>
    public int ChiselBands { get; init; } = 32;

    /// <summary>The chisel marks' cells per tile along the face (how far one runs before it wavers).</summary>
    public int ChiselCells { get; init; } = 3;

    /// <summary>A chisel mark's relief, meters.</summary>
    public double ChiselMeters { get; init; } = 0.004;

    /// <summary>The broad mottling's cells per tile.</summary>
    public int MottleCells { get; init; } = 3;

    /// <summary>How far the mottling's tone departs from the rock's, either way, a share of it.</summary>
    public double MottleTone { get; init; } = 0.07;

    /// <summary>The fine grain's relief, meters.</summary>
    public double GrainMeters { get; init; } = 0.005;

    /// <summary>The grain noise's first octave's cells per tile.</summary>
    public int GrainCells { get; init; } = 32;

    /// <summary>How much the grain shows in the tone.</summary>
    public double GrainTone { get; init; } = 0.14;

    /// <summary>The cells per tile of the field that says where vesicles gather.</summary>
    public int VesicleFieldCells { get; init; } = 4;

    /// <summary>Share of the face (0 to 1) where vesicles gather.</summary>
    public double VesicleFieldShare { get; init; } = 0.3;

    /// <summary>
    /// Small vesicles: cells per tile of the first of their two lattices (one per cell at most); the two sizes share no factor,
    /// so together they never fall in rows.
    /// </summary>
    public int VesicleCells { get; init; } = 61;

    /// <summary>Cells per tile of the second lattice of small vesicles.</summary>
    public int VesicleCellsOther { get; init; } = 83;

    /// <summary>Share of the small vesicles' cells (0 to 1), in each lattice, that hold one where they gather.</summary>
    public double VesicleShare { get; init; } = 0.18;

    /// <summary>A small vesicle's depth, meters.</summary>
    public double VesicleMeters { get; init; } = 0.008;

    /// <summary>Large vesicles: cells per tile (one per cell at most).</summary>
    public int LargeVesicleCells { get; init; } = 24;

    /// <summary>Share of the large vesicles' cells (0 to 1) that hold one where they gather.</summary>
    public double LargeVesicleShare { get; init; } = 0.12;

    /// <summary>A large vesicle's depth, meters.</summary>
    public double LargeVesicleMeters { get; init; } = 0.014;

    /// <summary>Hairline cracks: cells per tile of their web.</summary>
    public int CrackCells { get; init; } = 5;

    /// <summary>Share of the cracks' length that shows (0 to 1).</summary>
    public double CrackShare { get; init; } = 0.14;

    /// <summary>A hairline crack's half width, meters.</summary>
    public double CrackMeters { get; init; } = 0.008;

    /// <summary>A hairline crack's depth, meters.</summary>
    public double CrackDepthMeters { get; init; } = 0.01;

    /// <summary>The rock's roughness (0 to 1).</summary>
    public double Roughness { get; init; } = 0.82;

    /// <summary>How far the roughness departs from the rock's with the mottling, either way.</summary>
    public double RoughnessSpread { get; init; } = 0.05;

    /// <summary>The seed of every noise of the pattern: another seed gives another face of the same character.</summary>
    public int Seed { get; init; } = 31;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double tile = TileMeters;
        double u = TileNoise.Unit(alongMeters, tile);
        double v = TileNoise.Unit(upMeters, tile);

        double undulation = TileNoise.Fractal(u, v, UndulationCells, 2, Seed + 1) - 0.5;
        double chisel = TileNoise.Fractal(u * ChiselCells, v * ChiselBands, ChiselCells, ChiselBands, 2, Seed + 2) - 0.5;
        double mottle = TileNoise.Fractal(u, v, MottleCells, 3, Seed + 8) - 0.5;
        double grain = TileNoise.Fractal(u, v, GrainCells, 4, Seed + 3) - 0.5;

        double small = Math.Max(Vesicle(u, v, VesicleCells, VesicleShare, Seed + 4), Vesicle(u, v, VesicleCellsOther, VesicleShare, Seed + 10));
        double large = Vesicle(u, v, LargeVesicleCells, LargeVesicleShare, Seed + 5);

        // Hairline cracks, in short stretches.
        VoronoiCell web = TileNoise.Voronoi(u * CrackCells, v * CrackCells, CrackCells, Seed + 6);
        double showsNoise = TileNoise.Fractal(u, v, 8, 2, Seed + 7);
        double shows = TileNoise.Shows(showsNoise, CrackShare, 0.04);
        double crack = TileNoise.Line(web.Border * tile / CrackCells, CrackMeters) * shows;

        double height = (undulation * 2.0 * UndulationMeters) + (chisel * 2.0 * ChiselMeters) + (grain * 2.0 * GrainMeters)
            - (small * VesicleMeters) - (large * LargeVesicleMeters) - (crack * CrackDepthMeters);
        double tone = 1.0 + (mottle * 2.0 * MottleTone) + (chisel * 0.06) + (grain * GrainTone) - (small * 0.15) - (large * 0.3)
            - (crack * 0.3);
        double cavity = Math.Max(Math.Max(small * 0.5, large * 0.8), crack * 0.8);
        double rough = Roughness + (mottle * 2.0 * RoughnessSpread) + (cavity * 0.08);
        return new SurfacePoint(height, tone, cavity, Math.Clamp(rough, 0.0, 1.0));
    }

    /// <summary>
    /// The depth profile of a vesicle (1 at its bottom) on a lattice of <paramref name="cells"/> a side over the tile: at most one per
    /// cell, in <paramref name="share"/> of the cells where the vesicle field gathers them and in none elsewhere, each its own size
    /// and anywhere in its cell it fits, so they never line up.
    /// </summary>
    private double Vesicle(double u, double v, int cells, double share, int seed)
    {
        LatticeCell cell = TileNoise.Locate(u, v, cells);
        double field = TileNoise.Fractal(
            (cell.Wx + 0.5) / cells * VesicleFieldCells, (cell.Wy + 0.5) / cells * VesicleFieldCells, VesicleFieldCells, VesicleFieldCells, 2, Seed + 9);
        double gathered = TileNoise.Shows(field, VesicleFieldShare, 0.1);
        if (TileNoise.Hash(cell.Wx, cell.Wy, seed) >= share * gathered)
        {
            return 0.0;
        }

        double radius = 0.1 + (0.25 * TileNoise.Hash(cell.Wx, cell.Wy, seed + 3));
        double room = 1.0 - (2.0 * (radius + 0.03));
        double distance = cell.DistanceToCenter(TileNoise.Hash(cell.Wx, cell.Wy, seed + 1), TileNoise.Hash(cell.Wx, cell.Wy, seed + 2), room);
        return TileNoise.Line(distance, radius);
    }
}
