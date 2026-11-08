using Xunit;

namespace TheDeep.Tests;

public class VideoClockTests
{
    [Fact]
    public void FirstFrameIsDueAtStart()
    {
        var clock = new VideoClock(60, 60);
        Assert.Equal(1, clock.Advance(0.0));
        Assert.Equal(0, clock.Advance(0.001));
    }

    [Fact]
    public void FastGameFramesSkipUntilTheNextVideoFrame()
    {
        var clock = new VideoClock(60, 60);
        int total = 0;
        for (int i = 0; i <= 144; i++)
        {
            total += clock.Advance(i / 144.0); // one second of a 144 Hz game
        }

        Assert.Equal(61, total); // frames at 0/60 … 60/60 s
        Assert.Equal(61, clock.FramesEmitted);
    }

    [Fact]
    public void SlowGameFramesRepeatToKeepRealTime()
    {
        var clock = new VideoClock(60, 60);
        clock.Advance(0.0);
        Assert.Equal(2, clock.Advance(1.0 / 30.0)); // a 30 fps game fills two video frames per frame
        Assert.Equal(1.0 / 20.0, clock.DurationSeconds, 9);
    }

    [Fact]
    public void LongStallsAreCutDownToTheBurstLimit()
    {
        var clock = new VideoClock(60, 10);
        clock.Advance(0.0);
        Assert.Equal(10, clock.Advance(1.0)); // 60 due, 10 allowed
        Assert.Equal(50, clock.FramesCut);
        Assert.Equal(1, clock.Advance(1.0 + 1.0 / 60.0)); // then back to normal pacing
        Assert.Equal(0, clock.Advance(1.0 + 1.5 / 60.0));
    }
}
