using System;

namespace TheDeep;

/// <summary>
/// Old clear ice, as a tileable pattern (<see cref="ISurfacePattern"/>) for the ice material: what is frozen inside it, seen
/// through a glossy skin. Fracture planes split it into plates a little under a meter across, each crack a thin bright edge with
/// a pale wash on one side, where the plane runs back into the ice at a slant; the cracks fade out along their length, so some
/// end in the ice. Finer cracks cluster in patches between them. Small air bubbles rise in vertical trails and crowd in
/// horizontal bands, and milky veils lie frozen in, stretched along the face. The skin undulates a little and has matte
/// patches where it sublimated.
/// <para>
/// The ice is dark and what is in it is pale, so its <see cref="SurfacePoint.Tone"/> is 1 plus how white an inclusion shows
/// (0 to 1): the material mixes its dark body toward the rime by tone − 1 (<c>assets/shaders/ice.gdshader</c>). The inclusions
/// lie inside, so they have no relief, no cavity and no cell: the height is the skin's alone. The material shows the tile in
/// patches, two ways round, so it never repeats along a wall. The knobs are its properties (meters unless named otherwise).
/// </para>
/// </summary>
public sealed record IceSurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 4.0;

    /// <summary>Plates per tile side, the share of their borders that cracked (in stretches, from a noise of
    /// <see cref="CrackCells"/> per tile), and how far a crack strays from straight.</summary>
    public int PlateCells { get; init; } = 5;

    public double CrackShare { get; init; } = 0.75;

    public int CrackCells { get; init; } = 3;

    public double CrackWarpMeters { get; init; } = 0.05;

    /// <summary>A crack's bright edge (half width); the wash on its lit side: how bright at the edge and how far it reaches.</summary>
    public double CrackMeters { get; init; } = 0.012;

    public double Wash { get; init; } = 0.1;

    public double WashMeters { get; init; } = 0.06;

    /// <summary>Share of plates whose cracks wash them (the planes slanting under them).</summary>
    public double LitShare { get; init; } = 0.4;

    /// <summary>Finer cracks: cells per tile side, the share of their length cracked, their half width, how white, and the
    /// cells per tile side of the patches they cluster in.</summary>
    public int FineCells { get; init; } = 14;

    public double FineShare { get; init; } = 0.35;

    public double FineMeters { get; init; } = 0.008;

    public double Fine { get; init; } = 0.25;

    public int FinePatchCells { get; init; } = 4;

    /// <summary>Bubbles: cells per tile side (one bubble per cell at most), the share of cells holding one where they crowd most,
    /// and their radius at least and at most (a share of a cell).</summary>
    public int BubbleCells { get; init; } = 40;

    public double BubbleShare { get; init; } = 0.06;

    public double BubbleMinRadius { get; init; } = 0.14;

    public double BubbleMaxRadius { get; init; } = 0.32;

    /// <summary>Small bubbles, the same way, smaller and many more.</summary>
    public int SmallBubbleCells { get; init; } = 110;

    public double SmallBubbleShare { get; init; } = 0.04;

    /// <summary>Where bubbles crowd: vertical trails (columns per tile) and horizontal bands (bands per tile).</summary>
    public int TrailCells { get; init; } = 9;

    public int BandCells { get; init; } = 5;

    /// <summary>The frozen-in veils: how white at most, and their noise's cells per tile along and up the face.</summary>
    public double Veil { get; init; } = 0.08;

    public int VeilCellsAlong { get; init; } = 2;

    public int VeilCellsUp { get; init; } = 6;

    /// <summary>The skin's undulation: its relief and its first octave's cells per tile.</summary>
    public double RippleMeters { get; init; } = 0.01;

    public int RippleCells { get; init; } = 6;

    /// <summary>The glossy skin's roughness, the matte patches' and those patches' cells per tile.</summary>
    public double Roughness { get; init; } = 0.05;

    public double MatteRoughness { get; init; } = 0.32;

    public int MatteCells { get; init; } = 5;

    public int Seed { get; init; } = 4;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double tile = TileMeters;
        double u = TileNoise.Wrap(alongMeters, tile) / tile;
        double v = TileNoise.Wrap(upMeters, tile) / tile;

        // The plates' cracks: a bright edge, a wash on the lit side; they fade in and out along their length, so some end in
        // the ice. Every border of a lit plate washes it (fainter where the crack fades), so the wash has no edge of its own.
        double warp = CrackWarpMeters / tile * PlateCells;
        double wx = (TileNoise.Fractal(u * 8, v * 8, 8, 8, 3, Seed + 1) - 0.5) * 2.0 * warp;
        double wy = (TileNoise.Fractal(u * 8, v * 8, 8, 8, 3, Seed + 2) - 0.5) * 2.0 * warp;
        VoronoiCell plate = TileNoise.Voronoi((u * PlateCells) + wx, (v * PlateCells) + wy, PlateCells, Seed + 3);
        double meters = plate.Border * tile / PlateCells;
        bool lit = TileNoise.Hash(plate.Cell, 0, Seed + 5) < LitShare;
        double fade = Fade(u, v, CrackCells, CrackShare, Seed + 4);
        double wash = lit ? Wash * Math.Exp(-meters / WashMeters) * (0.4 + (0.6 * fade)) : 0.0;
        double crack = Math.Max(fade * TileNoise.Line(meters, CrackMeters), wash);

        // Finer cracks, in patches.
        double patch = TileNoise.SmoothStep(0.5, 0.68, TileNoise.Fractal(u * FinePatchCells, v * FinePatchCells, FinePatchCells, FinePatchCells, 2, Seed + 6));
        if (patch > 0.0)
        {
            VoronoiCell fine = TileNoise.Voronoi((u * FineCells) + (wx * 2.0), (v * FineCells) + (wy * 2.0), FineCells, Seed + 7);
            double fineFade = Fade(u, v, CrackCells * 3, FineShare, Seed + 8);
            crack = Math.Max(crack, Fine * patch * fineFade * TileNoise.Line(fine.Border * tile / FineCells, FineMeters));
        }

        // Bubbles in trails and bands.
        double bubble = Math.Max(
            Bubble(u, v, BubbleCells, BubbleShare, Seed + 9),
            Bubble(u, v, SmallBubbleCells, SmallBubbleShare, Seed + 10) * 0.8);

        // Veils, stretched along the face.
        double veil = Veil * TileNoise.SmoothStep(0.5, 0.78, TileNoise.Fractal(u * VeilCellsAlong, v * VeilCellsUp, VeilCellsAlong, VeilCellsUp, 4, Seed + 11));

        double white = Math.Clamp(Math.Max(crack, bubble) + veil, 0.0, 1.0);
        double height = (TileNoise.Fractal(u * RippleCells, v * RippleCells, RippleCells, RippleCells, 4, Seed + 12) - 0.5) * 2.0 * RippleMeters;
        double matte = TileNoise.SmoothStep(0.55, 0.8, TileNoise.Fractal(u * MatteCells, v * MatteCells, MatteCells, MatteCells, 3, Seed + 13));
        return new SurfacePoint(height, 1.0 + white, 0.0, TileNoise.Lerp(Roughness, MatteRoughness, matte));
    }

    /// <summary>How crowded bubbles are at a point (0 to 1): in a vertical trail or a horizontal band.</summary>
    private double Crowd(double u, double v)
    {
        double trail = TileNoise.SmoothStep(0.58, 0.74, TileNoise.Fractal(u * TrailCells, v * 2, TrailCells, 2, 2, Seed + 14));
        double band = TileNoise.SmoothStep(0.6, 0.78, TileNoise.Fractal(u * 2, v * BandCells, 2, BandCells, 3, Seed + 15));
        return Math.Max(trail, band);
    }

    /// <summary>A bubble's whiteness (a pale disc with a brighter rim) in a grid of <paramref name="cells"/> a side, at most one
    /// per cell, in <paramref name="share"/> of the cells where they crowd most (taken at the cell's center, so none is cut).</summary>
    private double Bubble(double u, double v, int cells, double share, int seed)
    {
        double x = u * cells;
        double y = v * cells;
        int cx = (int)Math.Floor(x);
        int cy = (int)Math.Floor(y);
        int wx = TileNoise.Wrap(cx, cells);
        int wy = TileNoise.Wrap(cy, cells);
        if (TileNoise.Hash(wx, wy, seed) >= share * Crowd((wx + 0.5) / cells, (wy + 0.5) / cells))
        {
            return 0.0;
        }

        double radius = TileNoise.Lerp(BubbleMinRadius, BubbleMaxRadius, TileNoise.Hash(wx, wy, seed + 3));
        double px = cx + 0.5 + ((TileNoise.Hash(wx, wy, seed + 1) - 0.5) * (1.0 - (2.0 * radius)));
        double py = cy + 0.5 + ((TileNoise.Hash(wx, wy, seed + 2) - 0.5) * (1.0 - (2.0 * radius)));
        double r = Math.Sqrt(((x - px) * (x - px)) + ((y - py) * (y - py))) / radius;
        double disc = 1.0 - TileNoise.SmoothStep(0.85, 1.0, r);
        double rim = TileNoise.SmoothStep(0.5, 0.85, r);
        return disc * (0.45 + (0.55 * rim));
    }

    /// <summary>Where cracks show (0 to 1): about <paramref name="share"/> of the face, in a smooth noise of
    /// <paramref name="cells"/> per tile, so a crack fades out over a few centimeters rather than stopping dead.</summary>
    private static double Fade(double u, double v, int cells, double share, int seed)
    {
        double noise = TileNoise.Fractal(u * cells, v * cells, cells, cells, 2, seed);
        double threshold = 0.5 - ((share - 0.5) * 0.5);
        return TileNoise.SmoothStep(threshold - 0.04, threshold + 0.04, noise);
    }
}
