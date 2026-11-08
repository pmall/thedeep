using System;
using Xunit;

namespace TheDeep.Tests;

public class FrameShimmerTests
{
    private static byte[] Luma(params byte[] grays)
    {
        var rgba = new byte[grays.Length * 4];
        for (int i = 0; i < grays.Length; i++)
        {
            rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = grays[i];
            rgba[i * 4 + 3] = (byte)(i * 37); // alpha is ignored
        }

        var luma = new byte[grays.Length];
        FrameShimmer.Luma(rgba, luma);
        return luma;
    }

    [Fact]
    public void LumaOfGrayIsItsValue() => Assert.Equal(new byte[] { 0, 100, 255 }, Luma(0, 100, 255));

    [Fact]
    public void IdenticalFramesDoNotChange()
    {
        byte[] a = Luma(10, 200, 30, 90);
        FrameShimmer.Change change = FrameShimmer.Compare(a, Luma(10, 200, 30, 90), 4, 1, 0.0, Span<byte>.Empty);
        Assert.Equal(0.0, change.MeanDifference);
        Assert.Equal(0, change.Pops);
    }

    [Fact]
    public void AStillCameraAllowsOnlyTheTolerance()
    {
        var heat = new byte[4];
        FrameShimmer.Change change = FrameShimmer.Compare(Luma(0, 0, 100, 100), Luma(0, 4, 100, 120), 4, 1, 0.0, heat);
        Assert.Equal(1, change.Pops);
        Assert.Equal(new byte[] { 0, 0, 0, 20 }, heat);
    }

    [Fact]
    public void AFilteredEdgeMovesWithTheCameraWithoutPopping()
    {
        // A ramp over two pixels, moved a sixteenth of a pixel: each pixel changes by its slope over sixteen.
        byte[] before = { 0, 0, 64, 128, 192, 192 };
        byte[] after = { 0, 0, 68, 132, 192, 192 };
        Assert.Equal(0, FrameShimmer.Compare(before, after, 6, 1, 1.0 / 16.0, Span<byte>.Empty).Pops);
    }

    [Fact]
    public void AnAliasedEdgeThatFlipsForASubPixelStepPops()
    {
        // A hard edge whose pixel flips whole for a sixteenth of a pixel.
        byte[] before = { 0, 0, 0, 200, 200, 200 };
        byte[] after = { 0, 0, 200, 200, 200, 200 };
        var heat = new byte[6];
        Assert.Equal(1, FrameShimmer.Compare(before, after, 6, 1, 1.0 / 16.0, heat).Pops);
        Assert.Equal(200, heat[2]);

        // The same flip for a whole pixel's step is the edge moving.
        Assert.Equal(0, FrameShimmer.Compare(before, after, 6, 1, 1.0, Span<byte>.Empty).Pops);
    }

    [Fact]
    public void ContrastIsReadAcrossRowsToo()
    {
        // 2x2: the bottom row is bright, the top-left pixel brightens by a sixteenth of its vertical contrast.
        byte[] before = { 0, 0, 160, 160 };
        byte[] after = { 10, 0, 160, 160 };
        Assert.Equal(0, FrameShimmer.Compare(before, after, 2, 2, 1.0 / 16.0, Span<byte>.Empty).Pops);
    }

    [Fact]
    public void ABlinkJumpsAndComesBack()
    {
        var heat = new byte[4];
        // Pixel 1 blinks; pixel 2 steps and stays; pixel 3 jumps back by less than the threshold.
        FrameShimmer.Change change = FrameShimmer.Blinks(Luma(50, 50, 50, 50), Luma(50, 90, 90, 90), Luma(50, 50, 90, 80), 4, heat);
        Assert.Equal(1, change.Pops);
        Assert.Equal(new byte[] { 0, 40, 0, 0 }, heat);
    }

    [Fact]
    public void APhaseSumsItsPopsAndKeepsTheWorstFrame()
    {
        var phase = new FrameShimmer.Phase();
        phase.Add(new FrameShimmer.Change(4, 3));
        phase.Add(new FrameShimmer.Change(2, 7));
        Assert.Equal(2, phase.Frames);
        Assert.Equal(3.0, phase.MeanDifference, 9);
        Assert.Equal(10, phase.Pops);
        Assert.Equal(7, phase.WorstFrame);
    }

    [Fact]
    public void ACameraCreepsUnderAPixelAFrame()
    {
        Assert.False(FrameShimmer.Creeps(0.0));
        Assert.True(FrameShimmer.Creeps(0.05));
        Assert.False(FrameShimmer.Creeps(1.5));
    }

    [Fact]
    public void RefusesShortFrames()
    {
        Assert.Throws<ArgumentException>(() => FrameShimmer.Compare(new byte[1], new byte[2], 2, 1, 0.0, Span<byte>.Empty));
        Assert.Throws<ArgumentException>(() => FrameShimmer.Luma(new byte[4], new byte[2]));
    }
}
