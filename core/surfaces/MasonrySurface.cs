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

    /// <summary>Blocks per course at least (at most <see cref="MaxBlocks"/>, which is at most <see cref="SurfaceMaps.Cells"/>).</summary>
    public int MinBlocks { get; init; } = 3;

    /// <summary>Blocks per course at most.</summary>
    public int MaxBlocks { get; init; } = 5;

    /// <summary>How far a joint between two blocks strays from even spacing, either way, a share of a block.</summary>
    public double BlockJitter { get; init; } = 0.2;

    /// <summary>Half width of a mortar joint, meters (flat, recessed by <see cref="JointDepthMeters"/>).</summary>
    public double JointMeters { get; init; } = 0.02;

    /// <summary>How far the mortar lies behind the blocks' faces, meters.</summary>
    public double JointDepthMeters { get; init; } = 0.03;

    /// <summary>The width over which a block's edge rounds, from the mortar up to its face, meters.</summary>
    public double ArrisMeters { get; init; } = 0.012;

    /// <summary>How far a chip bites into a block's arris at most, meters.</summary>
    public double ChipMeters { get; init; } = 0.06;

    /// <summary>How deep a chip's scar is, meters.</summary>
    public double ChipDepthMeters { get; init; } = 0.012;

    /// <summary>The chips' noise's first octave's cells per tile.</summary>
    public int ChipCells { get; init; } = 40;

    /// <summary>How far a block's face tilts, along and up, at most, as a slope (meters per meter).</summary>
    public double TiltSlope { get; init; } = 0.04;

    /// <summary>How far the middle of a block's face bulges out past its edges, meters.</summary>
    public double BulgeMeters { get; init; } = 0.01;

    /// <summary>How far a block stands proud of the wall's plane or behind it, at most, meters.</summary>
    public double ReliefMeters { get; init; } = 0.01;

    /// <summary>The tooled grain's relief, meters.</summary>
    public double GrainMeters { get; init; } = 0.004;

    /// <summary>The grain noise's first octave's cells per tile.</summary>
    public int GrainCells { get; init; } = 48;

    /// <summary>The grain noise's octaves: more add finer detail.</summary>
    public int GrainOctaves { get; init; } = 4;

    /// <summary>A small pit's depth, meters.</summary>
    public double PitMeters { get; init; } = 0.004;

    /// <summary>Pit cells per tile side, one pit per cell at most.</summary>
    public int PitCells { get; init; } = 128;

    /// <summary>Share of cells (0 to 1) pitted in the most weathered block; fewer in the others.</summary>
    public double PitShare { get; init; } = 0.12;

    /// <summary>How much the grain shows in the stone's tone.</summary>
    public double GrainTone { get; init; } = 0.3;

    /// <summary>How much paler a chip's fresh stone is, a share of the stone's tone.</summary>
    public double ChipTone { get; init; } = 0.12;

    /// <summary>The mortar's tone (1 is the stone's).</summary>
    public double MortarTone { get; init; } = 0.85;

    /// <summary>Roughness of the cut stone (0 to 1); each block departs from it by up to <see cref="RoughnessSpread"/>.</summary>
    public double Roughness { get; init; } = 0.8;

    /// <summary>How far a block's roughness departs from the cut stone's, either way (roughness runs 0 to 1).</summary>
    public double RoughnessSpread { get; init; } = 0.06;

    /// <summary>The mortar's roughness (0 to 1).</summary>
    public double MortarRoughness { get; init; } = 0.95;

    /// <summary>The chip noise's level (0 to 1) where a chip starts to bite.</summary>
    public double ChipFrom { get; init; } = 0.56;

    /// <summary>The chip noise's level (0 to 1) where a chip bites its deepest.</summary>
    public double ChipTo { get; init; } = 0.8;

    /// <summary>How far from a block's edges its face bulges up to full, meters.</summary>
    public double BulgeReachMeters { get; init; } = 0.2;

    /// <summary>How much darker the stone is at the bottom of a pit, a share of its tone.</summary>
    public double PitTone { get; init; } = 0.06;

    /// <summary>How much darker a weathered block is toward its upper edge, a share of its tone.</summary>
    public double WeatheringTone { get; init; } = 0.05;

    /// <summary>How much of the mortar's recess counts as cavity (0 to 1).</summary>
    public double MortarCavity { get; init; } = 0.85;

    /// <summary>How much of a chip's scar counts as cavity (0 to 1).</summary>
    public double ChipCavity { get; init; } = 0.15;

    /// <summary>How much of a pit's depth counts as cavity (0 to 1).</summary>
    public double PitCavity { get; init; } = 0.5;

    /// <summary>How much rougher a chip's broken stone is (roughness runs 0 to 1).</summary>
    public double ChipRoughness { get; init; } = 0.08;

    /// <summary>The seed of every noise of the pattern: another seed gives another wall of the same character.</summary>
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
        double chipNoise = TileNoise.Fractal(u, v, ChipCells, 3, Seed + 7);
        double bite = ChipMeters * TileNoise.SmoothStep(ChipFrom, ChipTo, chipNoise);
        double chipped = 1.0 - TileNoise.SmoothStep(JointMeters + (0.3 * bite), JointMeters + ArrisMeters + bite, toJoint) - mortar;

        // The block's face: tilted its own way, bulging, standing a little proud or back, weathered its own amount.
        double tiltAlong = (TileNoise.Hash(course, block, Seed + 2) - 0.5) * 2.0 * TiltSlope;
        double tiltUp = (TileNoise.Hash(course, block, Seed + 3) - 0.5) * 2.0 * TiltSlope;
        double proud = (TileNoise.Hash(course, block, Seed + 4) - 0.5) * 2.0 * ReliefMeters;
        double weathered = TileNoise.Hash(course, block, Seed + 5);
        double bulge = BulgeMeters * TileNoise.SmoothStep(0.0, BulgeReachMeters, toSide) * TileNoise.SmoothStep(0.0, BulgeReachMeters, toBed);
        double blockHeight = proud + bulge + (tiltAlong * (x - ((left + right) / 2.0))) + (tiltUp * (fromFloor - (courseMeters / 2.0)));

        double coarse = 0.6 + (0.8 * weathered) + chipped;
        double grain = TileNoise.Fractal(u, v, GrainCells, GrainOctaves, Seed + 6) - 0.5;
        double pit = TileNoise.Pit(u, v, PitCells, PitShare * weathered, Seed + 17);

        double stone = 1.0 - mortar;
        double stoneHeight = blockHeight + (grain * GrainMeters * coarse) - (pit * PitMeters) - (chipped * ChipDepthMeters);
        double height = TileNoise.Lerp(-JointDepthMeters, stoneHeight, stone);
        double stoneTone = 1.0 + (grain * GrainTone * coarse) + (chipped * ChipTone) - (pit * PitTone) - (WeatheringTone * weathered * (1.0 - (fromFloor / courseMeters)));
        double tone = TileNoise.Lerp(MortarTone, stoneTone, stone);
        double cavity = Math.Max(Math.Max(mortar * MortarCavity, chipped * ChipCavity), pit * PitCavity * stone);
        double stoneRough = Roughness + ((weathered - 0.5) * 2.0 * RoughnessSpread) + (chipped * ChipRoughness);
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
