using System;

namespace TheDeep;

/// <summary>
/// Cracked rock, as a tileable pattern (<see cref="ISurfacePattern"/>) laid over the rock material where the Drill can bore
/// (<c>assets/shaders/rock.gdshader</c>'s <c>fracture</c>): the face broken into chunks most of a meter across by deep, dark
/// fissures that wander and widen and narrow along their length, each chunk tilted its own way with its own tone, its edges
/// chipped back pale where it broke; finer cracks run into the chunks from the fissures and fade out inside them. The fissures
/// are wide enough to read at the gameplay camera's distance, so cracked rock stands apart from sound rock at a glance.
/// <para>
/// The set is an overlay: its <see cref="SurfacePoint.Tone"/> multiplies the rock's own, its relief adds to the rock's, and
/// its cavity is the fissures alone. The knobs are its properties (meters unless named otherwise).
/// </para>
/// </summary>
public sealed record CracksSurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 4.0;

    /// <summary>Chunks per tile side, and how far a fissure strays from straight.</summary>
    public int ChunkCells { get; init; } = 5;

    public double WarpMeters { get; init; } = 0.09;

    /// <summary>A fissure's half width, at least and at most (it varies along it), its depth, and how dark its bottom is.</summary>
    public double FissureMinMeters { get; init; } = 0.045;

    public double FissureMaxMeters { get; init; } = 0.085;

    public double FissureDepthMeters { get; init; } = 0.07;

    public double FissureTone { get; init; } = 0.06;

    /// <summary>The chipped band along a chunk's edge: how far it reaches beyond the fissure, how pale, and how far it slopes
    /// down into the fissure.</summary>
    public double ChipMeters { get; init; } = 0.07;

    public double ChipTone { get; init; } = 0.45;

    public double ChipDropMeters { get; init; } = 0.012;

    /// <summary>How far a chunk leans out of the face (a slope, meters per meter) and how far its tone departs from the rock's.</summary>
    public double TiltSlope { get; init; } = 0.07;

    public double ChunkContrast { get; init; } = 0.12;

    /// <summary>Finer cracks: cells per tile side, the share of their length that shows, their half width, depth and darkness.</summary>
    public int FineCells { get; init; } = 13;

    public double FineShare { get; init; } = 0.3;

    public double FineMeters { get; init; } = 0.016;

    public double FineDepthMeters { get; init; } = 0.025;

    public double FineTone { get; init; } = 0.65;

    /// <summary>Rough grit over the broken faces: its relief and its first octave's cells per tile.</summary>
    public double GritMeters { get; init; } = 0.006;

    public int GritCells { get; init; } = 40;

    /// <summary>The broken faces' roughness, and the fissures'.</summary>
    public double Roughness { get; init; } = 0.9;

    public double FissureRoughness { get; init; } = 1.0;

    public int Seed { get; init; } = 21;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double tile = TileMeters;
        double u = TileNoise.Wrap(alongMeters, tile) / tile;
        double v = TileNoise.Wrap(upMeters, tile) / tile;

        // The chunks: a warped Voronoi web, so the fissures wander.
        double warp = WarpMeters / tile * ChunkCells;
        double wx = (TileNoise.Fractal(u * 6, v * 6, 6, 6, 3, Seed + 1) - 0.5) * 2.0 * warp;
        double wy = (TileNoise.Fractal(u * 6, v * 6, 6, 6, 3, Seed + 2) - 0.5) * 2.0 * warp;
        VoronoiCell chunk = TileNoise.Voronoi((u * ChunkCells) + wx, (v * ChunkCells) + wy, ChunkCells, Seed + 3);
        double meters = chunk.Border * tile / ChunkCells;

        // The fissure: its width varies along it, its bottom dark and deep, its sides a V.
        double width = TileNoise.Lerp(FissureMinMeters, FissureMaxMeters, TileNoise.Fractal(u * 10, v * 10, 10, 10, 2, Seed + 4));
        double fissure = TileNoise.Line(meters, width);
        double core = TileNoise.Line(meters, width * 0.75);

        // The chipped edge: beyond the fissure, ragged, sloping down into it.
        double ragged = TileNoise.Fractal(u * 30, v * 30, 30, 30, 2, Seed + 5);
        double chip = TileNoise.Line(meters - width, ChipMeters * (0.4 + (1.2 * ragged))) * (1.0 - fissure);
        double onChunk = TileNoise.SmoothStep(width, width + ChipMeters, meters);

        // Each chunk leans its own way, from its site, and has its own tone.
        double tiltX = (TileNoise.Hash(chunk.Cell, 0, Seed + 6) - 0.5) * 2.0 * TiltSlope;
        double tiltY = (TileNoise.Hash(chunk.Cell, 1, Seed + 6) - 0.5) * 2.0 * TiltSlope;
        double tilt = -((chunk.ToSiteX * tiltX) + (chunk.ToSiteY * tiltY)) * tile / ChunkCells * onChunk;
        double chunkTone = (TileNoise.Hash(chunk.Cell, 2, Seed + 6) - 0.5) * 2.0 * ChunkContrast;

        // Finer cracks running in from the fissures, showing in stretches.
        VoronoiCell fineCell = TileNoise.Voronoi((u * FineCells) + (wx * 2.0), (v * FineCells) + (wy * 2.0), FineCells, Seed + 7);
        double showsNoise = TileNoise.Fractal(u * 9, v * 9, 9, 9, 2, Seed + 8);
        double threshold = 1.0 - FineShare;
        double shows = TileNoise.SmoothStep(threshold - 0.05, threshold + 0.05, showsNoise) * onChunk;
        double fine = TileNoise.Line(fineCell.Border * tile / FineCells, FineMeters) * shows;

        double grit = TileNoise.Fractal(u * GritCells, v * GritCells, GritCells, GritCells, 3, Seed + 9) - 0.5;
        double height = tilt - (fissure * FissureDepthMeters * (0.5 + (0.5 * core))) - (chip * ChipDropMeters)
            - (fine * FineDepthMeters) + (grit * 2.0 * GritMeters * onChunk);
        double tone = (1.0 + chunkTone + (grit * 0.15)) * (1.0 + (chip * ChipTone));
        tone = TileNoise.Lerp(tone, FissureTone, Math.Max(core, fissure * 0.85));
        tone *= 1.0 - (fine * (1.0 - FineTone));
        double cavity = Math.Max(fissure, fine * 0.7);
        double rough = TileNoise.Lerp(Roughness, FissureRoughness, cavity);
        return new SurfacePoint(height, tone, cavity, rough);
    }
}
