using Xunit;

namespace TheDeep.Tests;

public class PoseClockTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void WithoutAHoldEveryTickIsShown()
    {
        var clock = new PoseClock();
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(Dt, clock.Tick(Dt, changed: false), 6);
        }
    }

    [Fact]
    public void AHoldShowsOnePoseEveryFewTicksAndLosesNoTime()
    {
        var clock = new PoseClock { HoldSeconds = 4f / 60f };
        float shown = 0f;
        int poses = 0;
        for (int i = 0; i < 60; i++)
        {
            float advance = clock.Tick(Dt, changed: false);
            if (advance > 0f)
            {
                poses++;
                shown += advance;
                Assert.Equal(4f * Dt, advance, 4);
            }
        }

        Assert.Equal(15, poses);
        Assert.Equal(1f, shown, 4);
    }

    [Fact]
    public void AChangeOfPoseShowsAtOnce()
    {
        var clock = new PoseClock { HoldSeconds = 4f / 60f };
        Assert.Equal(0f, clock.Tick(Dt, changed: false));
        Assert.Equal(2f * Dt, clock.Tick(Dt, changed: true), 6);
        Assert.Equal(0f, clock.Tick(Dt, changed: false));
    }

    [Fact]
    public void AResetForgetsTheTimeHeld()
    {
        var clock = new PoseClock { HoldSeconds = 4f / 60f };
        clock.Tick(Dt, changed: false);
        clock.Tick(Dt, changed: false);
        clock.Reset();
        Assert.Equal(Dt, clock.Tick(Dt, changed: true), 6);
    }
}
