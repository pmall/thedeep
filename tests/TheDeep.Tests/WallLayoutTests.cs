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

    [Fact]
    public void MachineryGearsMeshWithoutClippingAndBanksStandOnTheWall()
    {
        int meshed = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var wall = WallLayout.Build(WallStyle.Machinery, 50f, 24f, seed);
            var gears = wall.Where(p => p.Piece == WallPiece.Gear).ToList();
            Assert.DoesNotContain(wall, p => p.Piece == WallPiece.Hub && !gears.Any(g => g.Position.X == p.Position.X && g.Position.Y == p.Position.Y));
            for (int i = 0; i < gears.Count; i++)
            {
                for (int j = i + 1; j < gears.Count; j++)
                {
                    // Two gears' pitch circles touch (meshed) or keep apart; never one through the other.
                    float gap = new System.Numerics.Vector2(gears[i].Position.X - gears[j].Position.X, gears[i].Position.Y - gears[j].Position.Y).Length()
                        - WallLayout.GearMesh * (gears[i].Size.X + gears[j].Size.X);
                    Assert.True(gap > -1e-3f, $"seed {seed}: two gears overlap by {-gap} m");
                    meshed += gap < 1e-3f ? 1 : 0;
                }
            }

            foreach (var bank in wall.Where(p => p.Piece == WallPiece.Bank))
            {
                Assert.True(bank.Size.Y > 2f * WallLayout.BankChest(bank.Size.Y), "a bank's pipes rise well over its chest");
                Assert.True(bank.Position.Y + bank.Size.Y <= 24f);
            }
        }

        Assert.True(meshed > 0, "no gear train meshes");
    }
}
