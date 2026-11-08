using System;
using Xunit;

namespace TheDeep.Tests;

public class FrameShimmerTests
{
    private static byte[] Frame(params byte[] grays)
    {
        var bytes = new byte[grays.Length * 4];
        for (int i = 0; i < grays.Length; i++)
        {
            bytes[i * 4] = bytes[i * 4 + 1] = bytes[i * 4 + 2] = grays[i];
            bytes[i * 4 + 3] = (byte)(i * 37); // alpha is ignored
        }

        return bytes;
    }

    [Fact]
    public void IdenticalFramesDoNotChange()
    {
        byte[] a = Frame(10, 200, 30, 90);
        FrameShimmer.Change change = FrameShimmer.Compare(a, Frame(10, 200, 30, 90), 4, Span<byte>.Empty);
        Assert.Equal(0.0, change.MeanDifference);
        Assert.Equal(0.0, change.FlickerShare);
    }

    [Fact]
    public void CountsFlickeringPixelsAndKeepsTheLargestChange()
    {
        var heat = new byte[4];
        FrameShimmer.Change change = FrameShimmer.Compare(Frame(0, 0, 100, 100), Frame(0, 10, 100, 200), 4, heat);
        Assert.Equal(0.25, change.FlickerShare, 6);
        Assert.InRange(change.MeanDifference, 27.0, 28.0);
        Assert.Equal(new byte[] { 0, 10, 0, 100 }, heat);

        FrameShimmer.Compare(Frame(0, 50, 100, 100), Frame(0, 0, 100, 100), 4, heat);
        Assert.Equal(50, heat[1]);
        Assert.Equal(100, heat[3]);
    }

    [Fact]
    public void APhaseAveragesItsChanges()
    {
        var phase = new FrameShimmer.Phase();
        phase.Add(new FrameShimmer.Change(4, 0.1));
        phase.Add(new FrameShimmer.Change(2, 0.3));
        Assert.Equal(2, phase.Frames);
        Assert.Equal(3.0, phase.MeanDifference, 9);
        Assert.Equal(0.2, phase.FlickerShare, 9);
    }

    [Fact]
    public void ACameraCreepsUnderAPixelAFrame()
    {
        Assert.False(FrameShimmer.Creeps(0.0));
        Assert.True(FrameShimmer.Creeps(0.05));
        Assert.False(FrameShimmer.Creeps(1.5));
    }

    [Fact]
    public void RefusesShortFrames() =>
        Assert.Throws<ArgumentException>(() => FrameShimmer.Compare(Frame(1), Frame(1, 2), 2, Span<byte>.Empty));
}
