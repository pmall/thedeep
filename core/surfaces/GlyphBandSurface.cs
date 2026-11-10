using System;

namespace TheDeep;

/// <summary>
/// The band of glyphs round a prayer wheel, as a tileable pattern (<see cref="ISurfacePattern"/>) for the machine material
/// (<c>assets/shaders/machine.gdshader</c>): a raised rim at either edge of the band, a groove beside each, and between them a row
/// of glyphs of the makers' script cut into the face (the same script as <c>assets/shaders/lib/glyph.gdshaderinc</c>: strokes
/// between the nodes of a 3x4 grid round a spine, often mirrored, sometimes under a halo).
/// <para>
/// The tile holds <see cref="Rows"/> bands, one above the other, each a row of <see cref="Columns"/> glyph cells
/// <see cref="CellMeters"/> wide; every cell holds a glyph or a gap, with a margin round it, so the material can lay any cell next to
/// any other: it fits a whole number of cells round a part's girth and folds each onto one of a row's, the row picked per machine.
/// Its <see cref="SurfacePoint.Tone"/> multiplies the band's color (the strokes and grooves darker, the rims paler), its cavity is the
/// strokes and the grooves (where rust settles), its roughness about <see cref="Roughness"/>, and its cell is 1 in a stroke and 0
/// elsewhere: the material reads it as a coverage, the glyphs that glow. The knobs are its properties (meters unless named
/// otherwise).
/// </para>
/// </summary>
public sealed record GlyphBandSurface : ISurfacePattern
{
    public double TileMeters => Columns * CellMeters;

    /// <summary>Glyph cells along a band, and bands up the tile: the tile is square, so <c>Columns * CellMeters</c> is
    /// <c>Rows * BandMeters</c>.</summary>
    public int Columns { get; init; } = 8;

    public int Rows { get; init; } = 4;

    /// <summary>A glyph cell's width along the band.</summary>
    public double CellMeters { get; init; } = 0.08;

    /// <summary>A band's height, rim to rim.</summary>
    public double BandMeters => TileMeters / Rows;

    /// <summary>A rim: its middle's distance from the band's edge, its half width, its height; the groove beside it (half width,
    /// depth) and how dark both grooves and strokes are.</summary>
    public double RimFromMeters { get; init; } = 0.014;

    public double RimMeters { get; init; } = 0.008;

    public double RimHeightMeters { get; init; } = 0.003;

    public double RimTone { get; init; } = 1.12;

    public double GrooveMeters { get; init; } = 0.003;

    public double GrooveDepthMeters { get; init; } = 0.0015;

    public double CutTone { get; init; } = 0.5;

    /// <summary>A glyph's height (centered between the rims, its halo included), a stroke's width as a share of it, how deep a
    /// stroke is cut, and the share of cells left empty (the gaps between words).</summary>
    public double GlyphMeters { get; init; } = 0.088;

    public double Stroke { get; init; } = 0.11;

    public double StrokeDepthMeters { get; init; } = 0.0012;

    public double Gaps { get; init; } = 0.15;

    /// <summary>The face's roughness, the strokes' (dull, so a glowing glyph reads by its light) and the rims' (polished).</summary>
    public double Roughness { get; init; } = 0.3;

    public double StrokeRoughness { get; init; } = 0.55;

    public double RimRoughness { get; init; } = 0.22;

    /// <summary>A glyph's width in glyph heights.</summary>
    private const double GlyphWidth = 0.6;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double along = TileNoise.Wrap(alongMeters, TileMeters);
        double up = TileNoise.Wrap(upMeters, TileMeters);
        int column = Math.Min((int)(along / CellMeters), Columns - 1);
        int row = Math.Min((int)(up / BandMeters), Rows - 1);
        double x = along - (column * CellMeters);
        double y = up - (row * BandMeters);

        // The rims, and a groove on either side of each.
        double toRim = Math.Min(Math.Abs(y - RimFromMeters), Math.Abs(y - (BandMeters - RimFromMeters)));
        double rim = 1.0 - TileNoise.SmoothStep(RimMeters - 0.0015, RimMeters + 0.0015, toRim);
        double crown = 1.0 - Math.Pow(Math.Min(1.0, toRim / RimMeters), 4.0);
        double groove = TileNoise.Line(toRim - RimMeters - GrooveMeters, GrooveMeters);

        // The glyph in this cell, in glyph heights from its corner: centered in the cell, and its strokes (0.035 to 1.085 glyph
        // heights up, the halo's top included) centered in the band.
        double glyph = GlyphMeters;
        double px = (x - ((CellMeters - (GlyphWidth * glyph)) / 2.0)) / glyph;
        double py = (y - ((BandMeters / 2.0) - (0.56 * glyph))) / glyph;
        double stroke = 0.0;
        if (TileNoise.Hash(column, row, 71) >= Gaps && px > -Stroke && px < GlyphWidth + Stroke && py > -Stroke && py < 1.0 + Stroke)
        {
            double d = GlyphDistance(px, py, column, row);
            stroke = 1.0 - TileNoise.SmoothStep((Stroke / 2.0) - 0.01, (Stroke / 2.0) + 0.01, d);
        }

        double height = (rim * crown * RimHeightMeters) - (groove * GrooveDepthMeters) - (stroke * StrokeDepthMeters);
        double tone = TileNoise.Lerp(1.0, RimTone, rim) * (1.0 - ((1.0 - CutTone) * Math.Max(groove, stroke)));
        double rough = TileNoise.Lerp(TileNoise.Lerp(Roughness, RimRoughness, rim), StrokeRoughness, stroke);
        return new SurfacePoint(height, tone, Math.Max(groove, stroke), rough, stroke > 0.5 ? 1 : 0);
    }

    /// <summary>
    /// Distance to the strokes of the glyph of cell (<paramref name="column"/>, <paramref name="row"/>), in glyph heights; the
    /// point in [0, 0.6] x [0, 1] covers the glyph. The rules of <c>glyph_distance</c> in <c>lib/glyph.gdshaderinc</c>, with this
    /// bake's own hash.
    /// </summary>
    private static double GlyphDistance(double x, double y, int column, int row)
    {
        double shape = TileNoise.Hash(column, row, 73);
        if (shape < 0.6)
        {
            // Mirrored about the spine.
            x = (GlyphWidth / 2.0) - Math.Abs(x - (GlyphWidth / 2.0));
        }

        const double stepX = 0.24;
        const double stepY = 0.8 / 3.0;
        const double originX = 0.06;
        const double originY = 0.1;
        double d = Segment(x, y, originX + stepX, originY, originX + stepX, originY + (3.0 * stepY));
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 2; c++)
            {
                double ax = originX + (c * stepX);
                double ay = originY + (r * stepY);
                int node = (((column * 4) + r) * 2) + c;
                if (TileNoise.Hash(node, row, 79) < 0.42)
                {
                    d = Math.Min(d, Segment(x, y, ax, ay, ax + stepX, ay));
                }

                if (r < 3)
                {
                    double v = TileNoise.Hash(node, row, 83);
                    if (v < 0.3)
                    {
                        d = Math.Min(d, Segment(x, y, ax, ay, ax, ay + stepY));
                    }
                    else if (v < 0.42)
                    {
                        d = Math.Min(d, Segment(x, y, ax, ay, ax + stepX, ay + stepY));
                    }
                }
            }
        }

        if (TileNoise.Hash(column, row, 89) < 0.3)
        {
            // A halo over the glyph.
            double hx = x - (GlyphWidth / 2.0);
            double hy = y - 0.9;
            d = Math.Min(d, Math.Abs(Math.Sqrt((hx * hx) + (hy * hy)) - 0.12));
        }

        return d;
    }

    private static double Segment(double x, double y, double ax, double ay, double bx, double by)
    {
        double pax = x - ax;
        double pay = y - ay;
        double bax = bx - ax;
        double bay = by - ay;
        double h = Math.Clamp(((pax * bax) + (pay * bay)) / ((bax * bax) + (bay * bay)), 0.0, 1.0);
        double dx = pax - (bax * h);
        double dy = pay - (bay * h);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
