using System;
using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class WallLayoutTests
{
    [Theory]
    [InlineData(WallStyle.Foundry)]
    [InlineData(WallStyle.Machinery)]
    [InlineData(WallStyle.Nave)]
    public void DressingStaysOnTheWallAndRepeats(WallStyle style)
    {
        var a = WallLayout.Build(style, 44f, 24f, 5);
        Assert.Equal(a, WallLayout.Build(style, 44f, 24f, 5));
        Assert.NotEqual(a, WallLayout.Build(style, 44f, 24f, 6));
        foreach (var p in a)
        {
            Assert.InRange(p.Position.X, -22f - 1e-3f, 22f + 1e-3f);
            Assert.InRange(p.Position.Y, 0f, 24f + 1e-3f);
            Assert.InRange(p.Position.Z, 0f, 1f);
        }
    }

    [Fact]
    public void PilastersShareTheWidthEvenly()
    {
        var xs = WallLayout.Pilasters(44f);
        Assert.Equal(-22f, xs[0], 3);
        Assert.Equal(22f, xs[^1], 3);
        var gaps = xs.Zip(xs.Skip(1), (l, r) => r - l).ToList();
        Assert.All(gaps, g => Assert.Equal(gaps[0], g, 3));
        Assert.InRange(gaps[0], WallLayout.Bay * 0.7f, WallLayout.Bay * 1.4f);
    }

    [Fact]
    public void FoundryMouthsGlowBehindTheirGrates()
    {
        var wall = WallLayout.Build(WallStyle.Foundry, 70f, 24f, 3);
        var mouths = wall.Where(p => p.Piece == WallPiece.Mouth).ToList();
        Assert.NotEmpty(mouths);
        foreach (var mouth in mouths)
        {
            Assert.Contains(wall, g => g.Piece == WallPiece.MouthGlow && MathF.Abs(g.Position.X - mouth.Position.X) < 1e-3f
                && g.Position.Z < mouth.Position.Z && g.Size == mouth.Size);
        }

        Assert.Contains(wall, p => p.Piece == WallPiece.Pipe);
        Assert.Contains(wall, p => p.Piece == WallPiece.Chain && p.Position.Y - p.Size.Y >= 0f);
    }

    [Fact]
    public void NaveArchesSpringFromTheCapitalsAndFrostHangsIcicles()
    {
        var wall = WallLayout.Build(WallStyle.Nave, 44f, 24f, 4);
        var capitals = wall.Where(p => p.Piece == WallPiece.Capital).ToList();
        var arcade = wall.Where(p => p.Piece == WallPiece.Arch && p.Size.X > 3f).ToList();
        Assert.NotEmpty(arcade);
        foreach (var arch in arcade)
        {
            // Both feet of the ring land on a capital's top.
            foreach (float foot in new[] { arch.Position.X - arch.Size.X * 0.5f, arch.Position.X + arch.Size.X * 0.5f })
            {
                Assert.Contains(capitals, c => MathF.Abs(c.Position.X - foot) < c.Size.X * 0.5f
                    && MathF.Abs(c.Position.Y + c.Size.Y * 0.5f - arch.Position.Y) < 1e-3f);
            }
        }

        Assert.Contains(wall, p => p.Piece == WallPiece.Recess);
        Assert.True(wall.Count(p => p.Piece == WallPiece.Course) >= 2, "a wall 24 m tall has a tier over the arcade");
        Assert.DoesNotContain(wall, p => p.Piece == WallPiece.Icicles);
        Assert.Contains(WallLayout.Build(WallStyle.Nave, 44f, 24f, 4, frost: 1f), p => p.Piece == WallPiece.Icicles);
    }
}
