using System;

namespace TheDeep;

/// <summary>
/// Cut stone laid in courses, as a tileable pattern (<see cref="ISurfacePattern"/>) for the rock material's masonry:
/// <see cref="Courses"/> courses of the same height stacked up the tile, each split into blocks of uneven length with a joint at
/// the tile's edge; each block's face tilted its own way and bulging a little, its arrises rounded and chipped here and there,
/// its face tooled with a fine grain and a few pits; recessed mortar joints between them. The material shuffles whole courses
/// and slides each along the face (<c>assets/shaders/rock.gdshader</c>), so the tile never shows twice in a row: every
/// horizontal joint is the same flat mortar for <see cref="JointMeters"/> on either side of its line, and any course can sit on
/// any other without a seam. Each block's tone is the material's too (from <see cref="SurfacePoint.Cell"/>, the block's index
/// in its course), so the tone pattern does not repeat with the tile. The knobs are its properties (meters unless named
/// otherwise).
/// </summary>
public sealed record MasonrySurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 4.0;

    /// <summary>Courses per tile: each is <see cref="TileMeters"/> over this high.</summary>
    public int Courses { get; init; } = 7;

    /// <summary>Blocks per course, at least and at most (at most <see cref="SurfaceMaps.Cells"/>), and how far a joint
    /// strays from even spacing, as a share of a block.</summary>
    public int MinBlocks { get; init; } = 3;

    public int MaxBlocks { get; init; } = 5;

    public double BlockJitter { get; init; } = 0.2;

    /// <summary>Half width of a mortar joint (flat, recessed by <see cref="JointDepthMeters"/>), and how far an arris rounds.</summary>
    public double JointMeters { get; init; } = 0.02;

    public double JointDepthMeters { get; init; } = 0.03;

    public double ArrisMeters { get; init; } = 0.012;

    /// <summary>How far a chip bites into a block's arris at most, how deep its scar is, and its noise's cells per tile.</summary>
    public double ChipMeters { get; init; } = 0.06;

    public double ChipDepthMeters { get; init; } = 0.012;

    public int ChipCells { get; init; } = 40;

    /// <summary>A block's face: its tilt (slope, along and up), how far it bulges, and how far it stands proud of the others.</summary>
    public double TiltSlope { get; init; } = 0.04;

    public double BulgeMeters { get; init; } = 0.01;

    public double ReliefMeters { get; init; } = 0.01;

    /// <summary>The tooled grain: its relief, its first octave's cells per tile and octaves.</summary>
    public double GrainMeters { get; init; } = 0.004;

    public int GrainCells { get; init; } = 48;

    public int GrainOctaves { get; init; } = 4;

    /// <summary>Small pits: their depth, cells per tile (one pit per cell at most), and the share of cells pitted in the most weathered block.</summary>
    public double PitMeters { get; init; } = 0.004;

    public int PitCells { get; init; } = 128;

    public double PitShare { get; init; } = 0.12;

    /// <summary>How much the grain shows in the tone; how much paler a chip's fresh stone is; the mortar's tone.</summary>
    public double GrainTone { get; init; } = 0.3;

    public double ChipTone { get; init; } = 0.12;

    public double MortarTone { get; init; } = 0.85;

    /// <summary>Roughness of the cut stone (each block departs from it by up to <see cref="RoughnessSpread"/>) and of the mortar.</summary>
    public double Roughness { get; init; } = 0.8;

    public double RoughnessSpread { get; init; } = 0.06;

    public double MortarRoughness { get; init; } = 0.95;

    public int Seed { get; init; } = 2;

    /// <summary>A course's height.</summary>
    public double CourseMeters => TileMeters / Courses;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double tile = TileMeters;
        double x = TileNoise.Wrap(alongMeters, tile);
        double y = TileNoise.Wrap(upMeters, tile);
        double u = x / tile;
        double v = y / tile;

        // The course and the block the point is in.
        double courseMeters = CourseMeters;
        int course = Math.Min((int)(y / courseMeters), Courses - 1);
        double fromFloor = y - (course * courseMeters);
        int blocks = BlocksIn(course);
        int block = 0;
        while (block < blocks - 1 && x >= BlockEdge(course, blocks, block + 1))
        {
            block++;
        }

        double left = BlockEdge(course, blocks, block);
        double right = BlockEdge(course, blocks, block + 1);
        double toSide = Math.Min(x - left, right - x);
        double toBed = Math.Min(fromFloor, courseMeters - fromFloor);
        double toJoint = Math.Min(toSide, toBed);

        // The mortar, up to the rounded arris; the arris chipped where the noise says, a shallow scar of paler broken stone (a
        // chip only ever bites the block, never the mortar).
        double mortar = 1.0 - TileNoise.SmoothStep(JointMeters, JointMeters + ArrisMeters, toJoint);
        double chipNoise = TileNoise.Fractal(u * ChipCells, v * ChipCells, ChipCells, ChipCells, 3, Seed + 7);
        double bite = ChipMeters * TileNoise.SmoothStep(0.56, 0.8, chipNoise);
        double chipped = 1.0 - TileNoise.SmoothStep(JointMeters + (0.3 * bite), JointMeters + ArrisMeters + bite, toJoint) - mortar;

        // The block's face: tilted its own way, bulging, standing a little proud or back, weathered its own amount.
        double tiltAlong = (TileNoise.Hash(course, block, Seed + 2) - 0.5) * 2.0 * TiltSlope;
        double tiltUp = (TileNoise.Hash(course, block, Seed + 3) - 0.5) * 2.0 * TiltSlope;
        double proud = (TileNoise.Hash(course, block, Seed + 4) - 0.5) * 2.0 * ReliefMeters;
        double weathered = TileNoise.Hash(course, block, Seed + 5);
        double bulge = BulgeMeters * TileNoise.SmoothStep(0.0, 0.2, toSide) * TileNoise.SmoothStep(0.0, 0.2, toBed);
        double blockHeight = proud + bulge + (tiltAlong * (x - ((left + right) / 2.0))) + (tiltUp * (fromFloor - (courseMeters / 2.0)));

        double coarse = 0.6 + (0.8 * weathered) + chipped;
        double grain = TileNoise.Fractal(u * GrainCells, v * GrainCells, GrainCells, GrainCells, GrainOctaves, Seed + 6) - 0.5;
        double pit = TileNoise.Pit(u, v, PitCells, PitShare * weathered, Seed + 17);

        double stone = 1.0 - mortar;
        double stoneHeight = blockHeight + (grain * GrainMeters * coarse) - (pit * PitMeters) - (chipped * ChipDepthMeters);
        double height = TileNoise.Lerp(-JointDepthMeters, stoneHeight, stone);
        double stoneTone = 1.0 + (grain * GrainTone * coarse) + (chipped * ChipTone) - (pit * 0.06) - (0.05 * weathered * (1.0 - (fromFloor / courseMeters)));
        double tone = TileNoise.Lerp(MortarTone, stoneTone, stone);
        double cavity = Math.Max(Math.Max(mortar * 0.85, chipped * 0.15), pit * 0.5 * stone);
        double stoneRough = Roughness + ((weathered - 0.5) * 2.0 * RoughnessSpread) + (chipped * 0.08);
        double rough = TileNoise.Lerp(MortarRoughness, stoneRough, stone);
        return new SurfacePoint(height, tone, cavity, Math.Clamp(rough, 0.0, 1.0), block);
    }

    /// <summary>How many blocks the course <paramref name="course"/> holds.</summary>
    public int BlocksIn(int course) =>
        MinBlocks + (int)(TileNoise.Hash(course, 0, Seed) * (MaxBlocks - MinBlocks + 1));

    /// <summary>Where the block <paramref name="block"/> of a course starts along the tile (meters): the first at the tile's edge.</summary>
    public double BlockEdge(int course, int blocks, int block)
    {
        if (block <= 0)
        {
            return 0.0;
        }

        if (block >= blocks)
        {
            return TileMeters;
        }

        return (block + ((TileNoise.Hash(course, block, Seed + 1) - 0.5) * 2.0 * BlockJitter)) * TileMeters / blocks;
    }
}
