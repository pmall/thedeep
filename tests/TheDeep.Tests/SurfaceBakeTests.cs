using System;
using Xunit;

namespace TheDeep.Tests;

public class SurfaceBakeTests
{
    [Theory]
    [InlineData(0.3, 0.7)]
    [InlineData(2.9, 5.15)]
    [InlineData(-1.25, 3.5)]
    public void ValueNoiseAndFractalRepeatEveryPeriod(double x, double y)
    {
        Assert.Equal(TileNoise.Value(x, y, 4, 6, 3), TileNoise.Value(x + 4, y, 4, 6, 3), 9);
        Assert.Equal(TileNoise.Value(x, y, 4, 6, 3), TileNoise.Value(x, y - 6, 4, 6, 3), 9);
        Assert.Equal(TileNoise.Fractal(x, y, 4, 6, 5, 3), TileNoise.Fractal(x - 8, y + 12, 4, 6, 5, 3), 9);
    }

    [Fact]
    public void NoiseStaysInItsRange()
    {
        for (int i = 0; i < 2000; i++)
        {
            double value = TileNoise.Value(i * 0.137, i * 0.291, 8, 8, 1);
            double hash = TileNoise.Hash(i, -i, 5);
            Assert.InRange(value, 0.0, 1.0);
            Assert.InRange(hash, 0.0, 1.0);
            Assert.True(hash < 1.0);
        }
    }

    [Fact]
    public void StrataRepeatEveryTile()
    {
        var strata = new StrataSurface();
        double tile = strata.TileMeters;
        for (int i = 0; i < 200; i++)
        {
            double along = i * 0.0731 % tile;
            double up = i * 0.1937 % tile;
            SurfacePoint here = strata.At(along, up);
            SurfacePoint there = strata.At(along + tile, up - tile);
            Assert.Equal(here.HeightMeters, there.HeightMeters, 6);
            Assert.Equal(here.Tone, there.Tone, 6);
            Assert.Equal(here.Cavity, there.Cavity, 6);
        }
    }

    [Theory]
    [MemberData(nameof(Patterns))]
    public void BakingTwiceWritesTheSameBytes(string name)
    {
        ISurfacePattern pattern = Pattern(name);
        SurfaceMaps first = SurfaceBaker.Bake(pattern, 48);
        SurfaceMaps second = SurfaceBaker.Bake(pattern, 48);
        Assert.Equal(first.Normal, second.Normal);
        Assert.Equal(first.Surface, second.Surface);
    }

    public static TheoryData<string> Patterns => new() { "strata", "masonry", "ice", "cracks", "panels", "coating", "glyphs" };

    /// <summary>Half a tile on, the bake is the same image rolled by half: its edges meet as its middle does, so it has no seam.</summary>
    [Theory]
    [MemberData(nameof(Patterns))]
    public void ABakedSetHasNoSeam(string name)
    {
        const int size = 64;
        ISurfacePattern pattern = Pattern(name);
        SurfaceMaps baked = SurfaceBaker.Bake(pattern, size);
        SurfaceMaps shifted = SurfaceBaker.Bake(new Shifted(pattern, pattern.TileMeters / 2.0), size);
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                int rolled = ((row + (size / 2)) % size * size) + ((column + (size / 2)) % size);
                int i = (row * size) + column;
                for (int channel = 0; channel < 3; channel++)
                {
                    Assert.InRange(Math.Abs(shifted.Normal[(i * 3) + channel] - baked.Normal[(rolled * 3) + channel]), 0, 1);
                }

                for (int channel = 0; channel < 4; channel++)
                {
                    Assert.InRange(Math.Abs(shifted.Surface[(i * 4) + channel] - baked.Surface[(rolled * 4) + channel]), 0, 1);
                }
            }
        }
    }

    [Fact]
    public void MasonryRepeatsEveryTile()
    {
        var masonry = new MasonrySurface();
        double tile = masonry.TileMeters;
        for (int i = 0; i < 200; i++)
        {
            double along = i * 0.0731 % tile;
            double up = i * 0.1937 % tile;
            SurfacePoint here = masonry.At(along, up);
            SurfacePoint there = masonry.At(along - tile, up + (2.0 * tile));
            Assert.Equal(here.HeightMeters, there.HeightMeters, 6);
            Assert.Equal(here.Tone, there.Tone, 6);
            Assert.Equal(here.Cavity, there.Cavity, 6);
            Assert.Equal(here.Cell, there.Cell);
        }
    }

    /// <summary>
    /// The material shuffles courses and slides each along (<c>rock.gdshader</c>): within a joint's half width of a course's
    /// floor, every course, anywhere along, is the same mortar, so any course sits on any other without a seam.
    /// </summary>
    [Fact]
    public void AnyCourseSitsOnAnyOther()
    {
        var masonry = new MasonrySurface();
        double course = masonry.CourseMeters;
        SurfacePoint mortar = masonry.At(0.37, 0.0);
        for (int k = 0; k < masonry.Courses; k++)
        {
            for (int i = 0; i < 40; i++)
            {
                double along = i * 0.0973;
                double offset = ((i % 9) - 4) / 4.0 * masonry.JointMeters * 0.99;
                SurfacePoint here = masonry.At(along, (k * course) + offset);
                Assert.Equal(mortar.HeightMeters, here.HeightMeters, 9);
                Assert.Equal(mortar.Tone, here.Tone, 9);
                Assert.Equal(mortar.Cavity, here.Cavity, 9);
                Assert.Equal(mortar.Roughness, here.Roughness, 9);
            }
        }
    }

    /// <summary>The material gives each block its tone from its cell, and a joint at the tile's edge in every course keeps a block whole across a repeat.</summary>
    [Fact]
    public void EachBlockIsOneCellAndEveryCourseHasAJointAtTheTileEdge()
    {
        var masonry = new MasonrySurface();
        double course = masonry.CourseMeters;
        for (int k = 0; k < masonry.Courses; k++)
        {
            int blocks = masonry.BlocksIn(k);
            Assert.InRange(blocks, 1, SurfaceMaps.Cells);
            double up = (k + 0.5) * course;
            Assert.True(masonry.At(0.001, up).Cavity > 0.8);
            Assert.True(masonry.At(masonry.TileMeters - 0.001, up).Cavity > 0.8);
            for (int b = 0; b < blocks; b++)
            {
                double left = masonry.BlockEdge(k, blocks, b);
                double right = masonry.BlockEdge(k, blocks, b + 1);
                Assert.True(right - left > 0.8 * course, $"course {k} block {b} is {right - left:F2} m long");
                for (int i = 1; i < 10; i++)
                {
                    Assert.Equal(b, masonry.At(left + ((right - left) * i / 10.0), (k + (i / 10.0)) * course).Cell);
                }
            }
        }
    }

    [Fact]
    public void BakedCellsReadBackWhole()
    {
        SurfaceMaps baked = SurfaceBaker.Bake(new MasonrySurface(), 64);
        for (int i = 3; i < baked.Surface.Length; i += 4)
        {
            int cell = (int)Math.Round(baked.Surface[i] / 16.0) - 1;
            Assert.InRange(cell, 0, SurfaceMaps.Cells - 1);
            Assert.NotEqual(0, baked.Surface[i]);
        }
    }

    private static ISurfacePattern Pattern(string name) => name switch
    {
        "strata" => new StrataSurface(),
        "masonry" => new MasonrySurface(),
        "cracks" => new CracksSurface(),
        "panels" => new PanelSurface(),
        "coating" => new CoatingSurface(),
        "glyphs" => new GlyphBandSurface(),
        _ => new IceSurface(),
    };

    [Fact]
    public void IceRepeatsEveryTileAndItsInclusionsArePale()
    {
        var ice = new IceSurface();
        double tile = ice.TileMeters;
        int white = 0;
        for (int i = 0; i < 400; i++)
        {
            double along = i * 0.0731 % tile;
            double up = i * 0.1937 % tile;
            SurfacePoint here = ice.At(along, up);
            SurfacePoint there = ice.At(along + (3.0 * tile), up - tile);
            Assert.Equal(here.HeightMeters, there.HeightMeters, 6);
            Assert.Equal(here.Tone, there.Tone, 6);
            Assert.Equal(here.Roughness, there.Roughness, 6);
            Assert.InRange(here.Tone, 1.0, SurfaceMaps.ToneScale);
            Assert.Equal(0.0, here.Cavity);
            white += here.Tone > 1.3 ? 1 : 0;
        }

        // Fractures and bubbles show, but most of the ice is clear.
        Assert.InRange(white, 8, 200);
    }

    [Fact]
    public void CracksRepeatEveryTileAndWebTheFace()
    {
        var cracks = new CracksSurface();
        double tile = cracks.TileMeters;
        int fissured = 0;
        for (int i = 0; i < 400; i++)
        {
            double along = i * 0.0731 % tile;
            double up = i * 0.1937 % tile;
            SurfacePoint here = cracks.At(along, up);
            SurfacePoint there = cracks.At(along - (2.0 * tile), up + tile);
            Assert.Equal(here.HeightMeters, there.HeightMeters, 6);
            Assert.Equal(here.Tone, there.Tone, 6);
            Assert.Equal(here.Cavity, there.Cavity, 6);
            Assert.InRange(here.Tone, 0.0, SurfaceMaps.ToneScale);
            fissured += here.Cavity > 0.5 ? 1 : 0;
        }

        // The fissures web the face, but most of it is broken rock between them.
        Assert.InRange(fissured, 12, 120);
    }

    /// <summary>
    /// The material folds panels of any size onto the tile's (<c>metal.gdshader</c>): a panel must be symmetric about its middle
    /// lines, and plain near them, every point there the same as its middle line's, so a longer edge can repeat it.
    /// </summary>
    [Fact]
    public void EveryPanelFoldsOntoItsCorner()
    {
        var panels = new PanelSurface();
        double side = panels.PanelMeters;
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            double ox = quadrant % 2 * side;
            double oy = quadrant / 2 * side;
            for (int i = 0; i < 300; i++)
            {
                double x = i * 0.0731 % side;
                double y = i * 0.1937 % side;
                AssertSame(panels.At(ox + x, oy + y), panels.At(ox + side - x, oy + y));
                AssertSame(panels.At(ox + x, oy + y), panels.At(ox + x, oy + side - y));
                double near = panels.ReachMeters + ((i % 10) / 10.0 * panels.FlatMeters);
                AssertSame(panels.At(ox + (side / 2.0), oy + y), panels.At(ox + near, oy + y));
                AssertSame(panels.At(ox + x, oy + (side / 2.0)), panels.At(ox + x, oy + near));
            }
        }

        // Rivets stand on some panels: their heads are the only cells.
        int heads = 0;
        for (int i = 0; i < 4000; i++)
        {
            heads += panels.At(i * 0.0731 % panels.TileMeters, i * 0.1937 % panels.TileMeters).Cell;
        }

        Assert.InRange(heads, 10, 400);
    }

    [Fact]
    public void CoatingRepeatsEveryTileAndRunsStreakSomeOfIt()
    {
        var coating = new CoatingSurface();
        double tile = coating.TileMeters;
        int wet = 0;
        for (int i = 0; i < 400; i++)
        {
            double along = i * 0.0731 % tile;
            double up = i * 0.1937 % tile;
            SurfacePoint here = coating.At(along, up);
            AssertSame(here, coating.At(along - tile, up + (2.0 * tile)));
            Assert.InRange(here.Tone, 0.5, 1.2);
            wet += here.Cavity > 0.5 ? 1 : 0;
        }

        Assert.InRange(wet, 8, 160);
    }

    /// <summary>
    /// The material folds any glyph cell of a band next to any other (<c>machine.gdshader</c>): along a cell's sides every cell is
    /// the same bare face and rims, so cells meet without a seam; and the strokes, the only cells, cover some of the band.
    /// </summary>
    [Fact]
    public void AnyGlyphCellSitsBesideAnyOther()
    {
        var glyphs = new GlyphBandSurface();
        Assert.Equal(glyphs.TileMeters, glyphs.Rows * glyphs.BandMeters, 9);
        int strokes = 0;
        for (int row = 0; row < glyphs.Rows; row++)
        {
            for (int i = 0; i < 40; i++)
            {
                double up = (row * glyphs.BandMeters) + (i / 40.0 * glyphs.BandMeters);
                SurfacePoint side = glyphs.At(0.002, i / 40.0 * glyphs.BandMeters);
                for (int column = 0; column < glyphs.Columns; column++)
                {
                    AssertSame(side, glyphs.At((column * glyphs.CellMeters) + 0.002, up));
                    AssertSame(side, glyphs.At(((column + 1) * glyphs.CellMeters) - 0.002, up));
                }
            }
        }

        for (int i = 0; i < 4000; i++)
        {
            SurfacePoint here = glyphs.At(i * 0.0731 % glyphs.TileMeters, i * 0.1937 % glyphs.TileMeters);
            AssertSame(here, glyphs.At((i * 0.0731 % glyphs.TileMeters) + glyphs.TileMeters, (i * 0.1937 % glyphs.TileMeters) - glyphs.TileMeters));
            strokes += here.Cell;
        }

        Assert.InRange(strokes, 200, 1200);
    }

    private static void AssertSame(SurfacePoint expected, SurfacePoint actual)
    {
        Assert.Equal(expected.HeightMeters, actual.HeightMeters, 6);
        Assert.Equal(expected.Tone, actual.Tone, 6);
        Assert.Equal(expected.Cavity, actual.Cavity, 6);
        Assert.Equal(expected.Roughness, actual.Roughness, 6);
        Assert.Equal(expected.Cell, actual.Cell);
    }

    [Fact]
    public void AFlatPatternBakesAFlatNormal()
    {
        SurfaceMaps flat = SurfaceBaker.Bake(new Flat(), 8);
        for (int i = 0; i < 64; i++)
        {
            Assert.Equal(new byte[] { 128, 128, 255 }, flat.Normal[(i * 3)..((i * 3) + 3)]);
            Assert.Equal(128, flat.Surface[i * 4]);
        }
    }

    [Fact]
    public void ARiseUpTheTileTiltsTheNormalDown()
    {
        // Height grows up the tile, so the face leans back: its normal points down (green under the middle).
        SurfaceMaps slope = SurfaceBaker.Bake(new Ramp(), 16);
        int middle = ((8 * 16) + 8) * 3;
        Assert.Equal(128, slope.Normal[middle]);
        Assert.True(slope.Normal[middle + 1] < 120);
    }

    /// <summary>A pattern moved by <paramref name="Offset"/> meters along and up the face.</summary>
    private sealed record Shifted(ISurfacePattern Inner, double Offset) : ISurfacePattern
    {
        public double TileMeters => Inner.TileMeters;

        public SurfacePoint At(double alongMeters, double upMeters) => Inner.At(alongMeters + Offset, upMeters - Offset);
    }

    private sealed record Flat : ISurfacePattern
    {
        public double TileMeters => 1.0;

        public SurfacePoint At(double alongMeters, double upMeters) => new(0.0, 1.0, 0.0, 0.9);
    }

    /// <summary>A gentle ramp up the middle of the tile (one period of a sine, so it still tiles).</summary>
    private sealed record Ramp : ISurfacePattern
    {
        public double TileMeters => 1.0;

        public SurfacePoint At(double alongMeters, double upMeters) => new(0.1 * Math.Sin(2.0 * Math.PI * (upMeters - 0.25)), 1.0, 0.0, 0.9);
    }
}
