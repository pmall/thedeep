using System;

namespace TheDeep;

/// <summary>
/// The worn coating of the metal, as a tileable pattern (<see cref="ISurfacePattern"/>) laid over each face of the metal material
/// (<c>assets/shaders/metal.gdshader</c>) along with its panels (<see cref="PanelSurface"/>): a faint mottle and smudges in the
/// gloss, shallow dents, short scratches through the coating to the darker metal, and the runs water and rust leave down a
/// wall, long and narrow.
/// <para>
/// Its <see cref="SurfacePoint.Tone"/> multiplies the coating's color, its roughness is the coating's dry roughness about
/// <see cref="Roughness"/>, and its cavity holds the runs (0 to 1), which the material shows only where something runs from: below a
/// panel's top seam, fading down it, as wet streaks and, sampled elsewhere in the tile, as rust drips. It has no cells. The knobs
/// are its properties (meters unless named otherwise).
/// </para>
/// </summary>
public sealed record CoatingSurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 2.0;

    /// <summary>The mottle in the coating's tone, and its first octave's cells per tile.</summary>
    public double Mottle { get; init; } = 0.05;

    /// <summary>The mottle noise's first octave's cells per tile.</summary>
    public int MottleCells { get; init; } = 5;

    /// <summary>Dents: cells per tile side (one dent per cell at most), the share of cells with one, their depth.</summary>
    public int DentCells { get; init; } = 12;

    /// <summary>Share of cells (0 to 1) that hold a dent.</summary>
    public double DentShare { get; init; } = 0.18;

    /// <summary>A dent's depth at its bottom, meters.</summary>
    public double DentMeters { get; init; } = 0.0015;

    /// <summary>Scratches: cells per tile side (each holds the middle of one at most), the share of cells with one, their
    /// length at least and at most (in cells), their half width and depth, how dark the metal they bare is, and how far they
    /// stray from level (radians; most scratches are made by things moving along).</summary>
    public int ScratchCells { get; init; } = 14;

    /// <summary>Share of cells (0 to 1) that hold the middle of a scratch.</summary>
    public double ScratchShare { get; init; } = 0.25;

    /// <summary>A scratch's length at least, in cells.</summary>
    public double ScratchMinLength { get; init; } = 0.6;

    /// <summary>A scratch's length at most, in cells.</summary>
    public double ScratchMaxLength { get; init; } = 1.5;

    /// <summary>A scratch's half width at its middle, meters; it tapers toward both ends.</summary>
    public double ScratchMeters { get; init; } = 0.0035;

    /// <summary>A scratch's depth, meters.</summary>
    public double ScratchDepthMeters { get; init; } = 0.0006;

    /// <summary>What the coating's tone is multiplied by in a scratch, for the metal it bares (below 1 darkens).</summary>
    public double ScratchTone { get; init; } = 0.8;

    /// <summary>How far a scratch strays from level at most, either way, radians.</summary>
    public double ScratchLean { get; init; } = 0.5;

    /// <summary>The runs: their noise's cells per tile along and up the face (narrow and long), the noise level where a run
    /// starts and where it is full, and the cells per tile of the broad field that leaves some stretches dry.</summary>
    public int RunCellsAlong { get; init; } = 28;

    /// <summary>The runs' noise's cells per tile up the face (few: a run is long).</summary>
    public int RunCellsUp { get; init; } = 2;

    /// <summary>The runs' noise's level (0 to 1) where a run starts.</summary>
    public double RunFrom { get; init; } = 0.53;

    /// <summary>The runs' noise's level (0 to 1) where a run is full.</summary>
    public double RunTo { get; init; } = 0.75;

    /// <summary>Cells per tile of the broad noise whose low stretches stay dry of runs.</summary>
    public int RunPatchCells { get; init; } = 4;

    /// <summary>The coating's roughness, how far smudges move it, their cells per tile, and the scratches' roughness.</summary>
    public double Roughness { get; init; } = 0.32;

    /// <summary>How far a smudge moves the coating's roughness, either way (roughness runs 0 to 1).</summary>
    public double Smudge { get; init; } = 0.07;

    /// <summary>The smudges' noise's first octave's cells per tile.</summary>
    public int SmudgeCells { get; init; } = 6;

    /// <summary>The roughness (0 to 1) of a scratch's bared metal.</summary>
    public double ScratchRoughness { get; init; } = 0.24;

    /// <summary>The seed of every noise of the pattern: another seed gives another coating of the same character.</summary>
    public int Seed { get; init; } = 37;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double tile = TileMeters;
        double u = TileNoise.Wrap(alongMeters, tile) / tile;
        double v = TileNoise.Wrap(upMeters, tile) / tile;

        double mottle = (TileNoise.Fractal(u * MottleCells, v * MottleCells, MottleCells, MottleCells, 4, Seed + 1) - 0.5) * 2.0;
        double dent = TileNoise.Pit(u, v, DentCells, DentShare, Seed + 2);
        double scratch = Scratch(u, v);

        double runs = TileNoise.SmoothStep(RunFrom, RunTo, TileNoise.Fractal(u * RunCellsAlong, v * RunCellsUp, RunCellsAlong, RunCellsUp, 3, Seed + 3));
        runs *= TileNoise.SmoothStep(0.3, 0.55, TileNoise.Fractal(u * RunPatchCells, v * RunPatchCells, RunPatchCells, RunPatchCells, 2, Seed + 4));

        double smudge = (TileNoise.Fractal(u * SmudgeCells, v * SmudgeCells, SmudgeCells, SmudgeCells, 3, Seed + 5) - 0.5) * 2.0;
        double height = -(dent * DentMeters) - (scratch * ScratchDepthMeters);
        double tone = (1.0 + (mottle * Mottle)) * TileNoise.Lerp(1.0, ScratchTone, scratch);
        double rough = TileNoise.Lerp(Roughness + (smudge * Smudge), ScratchRoughness, scratch);
        return new SurfacePoint(height, tone, runs, rough);
    }

    /// <summary>How deep in a scratch the point is (0 to 1): a short straight score, tapered at both ends, from each cell around
    /// it that holds one.</summary>
    private double Scratch(double u, double v)
    {
        double x = u * ScratchCells;
        double y = v * ScratchCells;
        int cx = (int)Math.Floor(x);
        int cy = (int)Math.Floor(y);
        double cellMeters = TileMeters / ScratchCells;
        double best = 0.0;
        for (int j = -1; j <= 1; j++)
        {
            for (int i = -1; i <= 1; i++)
            {
                int wx = TileNoise.Wrap(cx + i, ScratchCells);
                int wy = TileNoise.Wrap(cy + j, ScratchCells);
                if (TileNoise.Hash(wx, wy, Seed + 6) >= ScratchShare)
                {
                    continue;
                }

                double mx = cx + i + TileNoise.Hash(wx, wy, Seed + 7);
                double my = cy + j + TileNoise.Hash(wx, wy, Seed + 8);
                double angle = (TileNoise.Hash(wx, wy, Seed + 9) - 0.5) * 2.0 * ScratchLean;
                double half = TileNoise.Lerp(ScratchMinLength, ScratchMaxLength, TileNoise.Hash(wx, wy, Seed + 10)) * 0.5;
                double dx = Math.Cos(angle);
                double dy = Math.Sin(angle);

                // Along the score from its middle (in cells), and across it (in meters).
                double s = ((x - mx) * dx) + ((y - my) * dy);
                double across = Math.Abs(((x - mx) * -dy) + ((y - my) * dx)) * cellMeters;
                if (Math.Abs(s) >= half)
                {
                    continue;
                }

                double taper = 1.0 - TileNoise.SmoothStep(0.4, 1.0, Math.Abs(s) / half);
                best = Math.Max(best, TileNoise.Line(across, ScratchMeters * (0.4 + (0.6 * taper))) * taper);
            }
        }

        return best;
    }
}
