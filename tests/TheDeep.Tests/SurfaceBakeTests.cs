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

    [Fact]
    public void BakingTwiceWritesTheSameBytes()
    {
        var pattern = new StrataSurface { TileMeters = 1.0 };
        SurfaceMaps first = SurfaceBaker.Bake(pattern, 48);
        SurfaceMaps second = SurfaceBaker.Bake(pattern, 48);
        Assert.Equal(first.Normal, second.Normal);
        Assert.Equal(first.Surface, second.Surface);
    }

    public static TheoryData<string> Patterns => new() { "strata", "masonry" };

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

    private static ISurfacePattern Pattern(string name) => name == "strata" ? new StrataSurface() : new MasonrySurface();

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
