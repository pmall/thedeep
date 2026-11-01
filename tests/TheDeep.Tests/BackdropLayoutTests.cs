using System;
using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class BackdropLayoutTests
{
    private static readonly BackdropRow Row = new(-40f, 60f, -8f, 40f, 9f, 1f, 17);

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
        Assert.Contains(nave, p => p.Piece == BackdropPiece.Rib);
    }

    [Fact]
    public void FoundryTrussesSpanBetweenChimneys()
    {
        var foundry = BackdropLayout.Build(BackdropStyle.Foundry, Row);
        var dims = BackdropDims.For(Row);
        var chimneys = foundry.Where(p => p.Piece == BackdropPiece.Chimney).Select(p => p.Position.X).OrderBy(x => x).ToList();
        Assert.True(chimneys.Count >= 4);
        foreach (var chord in foundry.Where(p => p.Piece == BackdropPiece.Chord))
        {
            float left = chord.Position.X - chord.Length * 0.5f - dims.ChimneyRadius;
            float right = chord.Position.X + chord.Length * 0.5f + dims.ChimneyRadius;
            Assert.Contains(chimneys, x => MathF.Abs(x - left) < 1e-3f);
            Assert.Contains(chimneys, x => MathF.Abs(x - right) < 1e-3f);
        }

        Assert.Contains(foundry, p => p.Piece == BackdropPiece.Brace);
        Assert.Contains(foundry, p => p.Piece == BackdropPiece.Chain && p.Length > 0f);
    }
}
