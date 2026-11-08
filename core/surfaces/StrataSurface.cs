using System;

namespace TheDeep;

/// <summary>
/// Sedimentary rock, as a tileable pattern (<see cref="ISurfacePattern"/>) for the rock material: beds of uneven thickness, each
/// proud of the next by its own amount and with its own tone, joined by grooved joints that come and go along their length;
/// their lips ragged; some beds split once more by a finer joint, the fine ones laminated; lumps, a grain stretched along the beds
/// and small pits, all coarser in some beds than others. No fault crosses the tile: one would come back every tile as a seam, so
/// the faults are the material's, at world scale (<c>assets/shaders/rock.gdshader</c>). The knobs are its properties (meters
/// unless named otherwise); the defaults are the rock of every biome.
/// </summary>
public sealed record StrataSurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 4.0;

    /// <summary>Beds per tile, stacked up it. Each bed's lower joint is moved by up to <see cref="BedJitter"/> of a mean bed.</summary>
    public int Beds { get; init; } = 6;

    public double BedJitter { get; init; } = 0.35;

    /// <summary>How far the beds wave up and down, at most.</summary>
    public double WarpMeters { get; init; } = 0.25;

    /// <summary>Half widths of a joint's groove: into the bed above it and into the bed below it (the lower lip is the wider).</summary>
    public double JointAboveMeters { get; init; } = 0.045;

    public double JointBelowMeters { get; init; } = 0.07;

    public double JointDepthMeters { get; init; } = 0.03;

    /// <summary>Noise cells per tile along a joint for where it shows and how wide it is.</summary>
    public int JointCells { get; init; } = 3;

    /// <summary>Share of beds split once more, the split joint's half width and its strength against a bed joint.</summary>
    public double SplitShare { get; init; } = 0.45;

    public double SplitMeters { get; init; } = 0.03;

    public double Split { get; init; } = 0.65;

    /// <summary>How far a bed stands proud of the face, at most, and how much its middle bulges.</summary>
    public double ReliefMeters { get; init; } = 0.035;

    public double BulgeMeters { get; init; } = 0.012;

    /// <summary>Thin layers inside a bed: their relief, and how many per bed at most.</summary>
    public double LaminaMeters { get; init; } = 0.0025;

    public int Laminae { get; init; } = 9;

    /// <summary>How far a joint's lips stray from its line, and the cells per tile of that noise's first octave.</summary>
    public double RaggedMeters { get; init; } = 0.014;

    public int RaggedCells { get; init; } = 24;

    /// <summary>Lumps on the beds' faces: their relief and their first octave's cells per tile.</summary>
    public double LumpMeters { get; init; } = 0.02;

    public int LumpCells { get; init; } = 10;

    /// <summary>The fine grain: its relief, its first octave's cells per tile along the face (twice as many up it), and octaves.</summary>
    public double GrainMeters { get; init; } = 0.02;

    public int GrainCells { get; init; } = 24;

    public int GrainOctaves { get; init; } = 4;

    /// <summary>Small pits: their depth, cells per tile (one pit per cell at most), and the share of cells pitted in the coarsest bed.</summary>
    public double PitMeters { get; init; } = 0.006;

    public int PitCells { get; init; } = 96;

    public double PitShare { get; init; } = 0.2;

    /// <summary>Tone difference between beds, and how much the grain shows in the tone.</summary>
    public double BedContrast { get; init; } = 0.18;

    public double GrainTone { get; init; } = 0.35;

    /// <summary>The dry rock's roughness, and how far a bed's departs from it.</summary>
    public double Roughness { get; init; } = 0.88;

    public double RoughnessSpread { get; init; } = 0.06;

    public int Seed { get; init; } = 1;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double tile = TileMeters;
        double u = TileNoise.Wrap(alongMeters, tile) / tile;
        double v = TileNoise.Wrap(upMeters, tile) / tile;

        // The beds wave as they run; their lips are ragged.
        double warp = TileNoise.Fractal(u * 2.0, v * 2.0, 2, 2, 3, Seed + 1) - 0.5;
        double ragged = TileNoise.Fractal(u * RaggedCells, v * RaggedCells, RaggedCells, RaggedCells, 3, Seed + 2) - 0.5;
        double bedUp = TileNoise.Wrap(upMeters + (warp * 2.0 * WarpMeters) + (ragged * 2.0 * RaggedMeters), tile);

        // The bed the point is in: between its lower joint and the next one up (the last bed wraps round to the first).
        int bed = Beds - 1;
        for (int k = 0; k < Beds; k++)
        {
            if (bedUp >= BedFloor(k))
            {
                bed = k;
            }
        }

        double floor = BedFloor(bed);
        if (floor > bedUp)
        {
            floor -= tile;
        }

        int next = (bed + 1) % Beds;
        double ceiling = next == 0 ? BedFloor(0) + tile : BedFloor(next);
        double fromFloor = bedUp - floor;
        double toCeiling = ceiling - bedUp;
        double thickness = ceiling - floor;
        double within = fromFloor / thickness;
        double bedHash = TileNoise.Hash(bed, 1, Seed + 3);
        double coarse = TileNoise.Hash(bed, 2, Seed + 3);

        // Joints: a bed's lower joint is the joint `bed`, its upper the joint `next`, so each joint is the same from both sides.
        double joint = Math.Max(
            JointAt(bed, u, fromFloor, JointAboveMeters),
            JointAt(next, u, toCeiling, JointBelowMeters));
        if (bedHash < SplitShare && thickness > 4.0 * SplitMeters)
        {
            double split = floor + (thickness * (0.3 + (0.4 * TileNoise.Hash(bed, 3, Seed + 3))));
            double shows = TileNoise.SmoothStep(0.3, 0.55, TileNoise.Value(u * JointCells, Beds + bed, JointCells, 2 * Beds, Seed + 5));
            joint = Math.Max(joint, TileNoise.Line(bedUp - split, SplitMeters) * Split * shows);
        }

        // The bed stands proud, meeting its neighbors at the joints (the coarse beds weathered rounder), its middle bulging.
        double edge = 1.0 + coarse;
        double inBed = TileNoise.SmoothStep(0.0, JointAboveMeters * edge, fromFloor) * TileNoise.SmoothStep(0.0, JointBelowMeters * edge, toCeiling);
        double relief = ((bedHash * ReliefMeters) + (BulgeMeters * coarse * Math.Sin(Math.PI * within))) * inBed;

        // Laminae in the fine beds, fading in and out along them.
        int laminae = 2 + (int)((1.0 - coarse) * (Laminae - 1));
        double laminaShows = TileNoise.Value(u * 8.0, bed, 8, Beds, Seed + 9) * (1.0 - coarse);
        double lamina = Math.Sin(2.0 * Math.PI * within * laminae) * laminaShows;

        // Lumps, a grain stretched along the beds (sediment, not concrete) and pits: the coarse beds take more of each.
        double roughness = 0.4 + coarse;
        double lumps = TileNoise.Fractal(u * LumpCells, v * LumpCells, LumpCells, LumpCells, 3, Seed + 10) - 0.5;
        double grain = TileNoise.Fractal(u * GrainCells, v * GrainCells * 2.0, GrainCells, GrainCells * 2, GrainOctaves, Seed + 11) - 0.5;
        double pit = TileNoise.Pit(u, v, PitCells, PitShare * coarse, Seed + 17);

        double height = relief + (lamina * LaminaMeters) + (((lumps * LumpMeters) + (grain * GrainMeters)) * roughness)
            - (joint * JointDepthMeters) - (pit * PitMeters);
        double tone = 1.0 + ((bedHash - 0.5) * 2.0 * BedContrast) + (((grain * GrainTone) + (lumps * 0.2)) * roughness) + (lamina * 0.03)
            - (pit * 0.08);
        double cavity = Math.Max(joint, pit * 0.5);
        double rough = Roughness + ((coarse - 0.5) * 2.0 * RoughnessSpread) + (cavity * 0.05);
        return new SurfacePoint(height, tone, cavity, Math.Clamp(rough, 0.0, 1.0));
    }

    /// <summary>Where the bed <paramref name="k"/>'s lower joint lies up the tile (meters), before the warp.</summary>
    private double BedFloor(int k)
    {
        double mean = TileMeters / Beds;
        return (k + 0.5 + ((TileNoise.Hash(k, 0, Seed + 3) - 0.5) * 2.0 * BedJitter)) * mean;
    }

    /// <summary>A joint's groove at <paramref name="distanceMeters"/> from it on one side: it comes and goes, and its width varies along it.</summary>
    private double JointAt(int joint, double u, double distanceMeters, double halfWidthMeters)
    {
        double shows = TileNoise.SmoothStep(0.25, 0.55, TileNoise.Value(u * JointCells, joint, JointCells, 2 * Beds, Seed + 5));
        double width = halfWidthMeters * (0.6 + (0.8 * TileNoise.Value(u * JointCells * 4, joint, JointCells * 4, Beds, Seed + 6)));
        return TileNoise.Line(distanceMeters, width) * shows;
    }
}
