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

    /// <summary>Half a tile on, the bake is the same image rolled by half: its edges meet as its middle does, so it has no seam.</summary>
    [Fact]
    public void ABakedSetHasNoSeam()
    {
        const int size = 64;
        var strata = new StrataSurface();
        SurfaceMaps baked = SurfaceBaker.Bake(strata, size);
        SurfaceMaps shifted = SurfaceBaker.Bake(new Shifted(strata, strata.TileMeters / 2.0), size);
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                int rolled = (((row + (size / 2)) % size * size) + ((column + (size / 2)) % size)) * 3;
                int i = ((row * size) + column) * 3;
                for (int channel = 0; channel < 3; channel++)
                {
                    Assert.InRange(Math.Abs(shifted.Normal[i + channel] - baked.Normal[rolled + channel]), 0, 1);
                    Assert.InRange(Math.Abs(shifted.Surface[i + channel] - baked.Surface[rolled + channel]), 0, 1);
                }
            }
        }
    }

    [Fact]
    public void AFlatPatternBakesAFlatNormal()
    {
        SurfaceMaps flat = SurfaceBaker.Bake(new Flat(), 8);
        for (int i = 0; i < flat.Normal.Length; i += 3)
        {
            Assert.Equal(new byte[] { 128, 128, 255 }, flat.Normal[i..(i + 3)]);
            Assert.Equal(128, flat.Surface[i]);
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
