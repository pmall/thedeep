using System;
using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class BackdropLayoutTests
{
    private static readonly BackdropRow Row = new(-40f, 60f, -30f, 50f, 0f, 20f, 9f, 1f, 17);

    [Theory]
    [InlineData(BackdropStyle.Nave)]
    [InlineData(BackdropStyle.Stacks)]
    [InlineData(BackdropStyle.Foundry)]
    [InlineData(BackdropStyle.Machinery)]
    public void EachStyleFillsTheRowDeterministically(BackdropStyle style)
    {
        var a = BackdropLayout.Build(style, Row);
        var b = BackdropLayout.Build(style, Row);
        Assert.NotEmpty(a);
        Assert.Equal(a, b);
        // Spread over the whole width, nothing far outside it.
        Assert.True(a.Min(p => p.Position.X) < Row.MinX + Row.Pitch);
        Assert.True(a.Max(p => p.Position.X) > Row.MaxX - Row.Pitch);
        Assert.All(a, p => Assert.InRange(p.Position.X, Row.MinX - 2f * Row.Pitch, Row.MaxX + 2f * Row.Pitch));
        Assert.All(a, p => Assert.True(p.Position.Y >= Row.BaseY - 1f));
        Assert.NotEqual(a, BackdropLayout.Build(style, Row with { Seed = 18 }));
    }

    [Fact]
    public void NaveRisesOutOfFrameAndItsArchesRestOnPiers()
    {
        var nave = BackdropLayout.Build(BackdropStyle.Nave, Row);
        var dims = BackdropDims.For(Row);
        Assert.Contains(nave, p => p.Piece == BackdropPiece.UpperPier && p.Position.Y + dims.UpperHeight > Row.TopY - 15f);
        var piers = nave.Where(p => p.Piece is BackdropPiece.Pier or BackdropPiece.UpperPier).ToList();
        foreach (var arch in nave.Where(p => p.Piece is BackdropPiece.PointedArch or BackdropPiece.UpperArch))
        {
            // A pier tier stands under each end of every arch, its top at the springing line.
            float height = arch.Piece == BackdropPiece.PointedArch ? dims.PierHeight : dims.UpperHeight;
            foreach (float end in new[] { arch.Position.X - Row.Pitch * 0.5f, arch.Position.X + Row.Pitch * 0.5f })
            {
                Assert.Contains(piers, p => MathF.Abs(p.Position.X - end) < 1e-3f && MathF.Abs(p.Position.Y + height - arch.Position.Y) < 1e-3f);
            }
        }

        // Some piers broken off, some arches fallen: not a perfect grid.
        var perPier = piers.GroupBy(p => MathF.Round(p.Position.X, 2)).Select(g => g.Count()).ToList();
        int tiers = perPier.Max();
        int arches = nave.Count(p => p.Piece is BackdropPiece.PointedArch or BackdropPiece.UpperArch);
        Assert.True(tiers >= 2);
        Assert.Contains(perPier, n => n < tiers);
        Assert.True(arches < (perPier.Count - 1) * tiers);
        // A few shafts up the piers' faces, upright, from the arcade's springing up.
        var shafts = nave.Where(p => p.Piece == BackdropPiece.Shaft).ToList();
        Assert.NotEmpty(shafts);
        Assert.True(shafts.Count < perPier.Count / 2);
        Assert.All(shafts, s => Assert.Equal(0f, s.RollZ));
        Assert.All(shafts, s => Assert.True(s.Position.Y - s.Length * 0.5f >= Row.BaseY + dims.PierHeight - 1e-3f));
    }

    [Fact]
    public void FoundryGantriesSpanBetweenStacksOverTheirFurnaces()
    {
        var foundry = BackdropLayout.Build(BackdropStyle.Foundry, Row);
        var dims = BackdropDims.For(Row);
        var stacks = foundry.Where(p => p.Piece == BackdropPiece.Stack).ToList();
        Assert.True(stacks.Count >= 4);
        foreach (var stack in stacks)
        {
            // Each stack rises from a hood on a furnace that stands from the row's foot up past the room's floor.
            Assert.Contains(foundry, h => h.Piece == BackdropPiece.Hood && MathF.Abs(h.Position.X - stack.Position.X) < 1e-3f
                && MathF.Abs(h.Position.Y + dims.HoodHeight * 0.5f - stack.Position.Y) < 1e-3f);
            var furnace = Assert.Single(foundry, f => f.Piece == BackdropPiece.Furnace && MathF.Abs(f.Position.X - stack.Position.X) < 1e-3f);
            Assert.Equal(Row.BaseY, furnace.Position.Y - furnace.Length * 0.5f, 3);
            Assert.True(furnace.Position.Y + furnace.Length * 0.5f > Row.FloorY);
        }

        var xs = stacks.Select(p => p.Position.X).ToList();
        foreach (var chord in foundry.Where(p => p.Piece == BackdropPiece.Chord))
        {
            float left = chord.Position.X - chord.Length * 0.5f - dims.StackRadius;
            float right = chord.Position.X + chord.Length * 0.5f + dims.StackRadius;
            Assert.Contains(xs, x => MathF.Abs(x - left) < 1e-3f);
            Assert.Contains(xs, x => MathF.Abs(x - right) < 1e-3f);
        }

        Assert.Contains(foundry, p => p.Piece == BackdropPiece.Brace);
        Assert.Contains(foundry, p => p.Piece == BackdropPiece.Tank);
        Assert.Contains(foundry, p => p.Piece == BackdropPiece.Duct);
        Assert.Contains(foundry, p => p.Piece == BackdropPiece.Chain && p.Length > 0f);
        // A temple's pieces never; furnace mouths only in a row that glows, and few of them.
        Assert.DoesNotContain(foundry, p => p.Piece is BackdropPiece.Pier or BackdropPiece.UpperPier or BackdropPiece.PointedArch or BackdropPiece.FurnaceMouth);
        var glowing = BackdropLayout.Build(BackdropStyle.Foundry, Row with { Glows = true });
        int mouths = glowing.Count(p => p.Piece == BackdropPiece.FurnaceMouth);
        Assert.InRange(mouths, 1, stacks.Count / 2);
    }

    [Fact]
    public void AnUnknownStyleThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => BackdropLayout.Build((BackdropStyle)99, Row));

    [Fact]
    public void ARowFillsTheWidestViewAtItsDepth()
    {
        var spec = new BackdropRowSpec(24f, 12f, 1.3f);
        var min = new System.Numerics.Vector2(0f, 0f);
        var max = new System.Numerics.Vector2(40f, 20f);
        BackdropRow row = spec.Behind(min, max, 90f, 0.55f, 0.27f, 5);
        // A camera 90 m out, anywhere over the bounds, sees no end of the row, no foot, no top.
        Assert.True(row.MinX <= min.X - (90f + 24f) * 0.55f + 1e-3f);
        Assert.True(row.MaxX >= max.X + (90f + 24f) * 0.55f - 1e-3f);
        Assert.True(row.BaseY <= min.Y - (90f + 24f) * 0.27f + 1e-3f);
        Assert.True(row.TopY >= max.Y + (90f + 24f) * 0.27f - 1e-3f);
        Assert.Equal((min.Y, max.Y), (row.FloorY, row.RoofY));
        Assert.Equal((spec.Pitch, spec.Scale), (row.Pitch, row.Scale));
        Assert.Equal(3, BackdropRowSpec.Defaults.Count);
    }
}
