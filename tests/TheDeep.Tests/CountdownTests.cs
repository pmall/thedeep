using Xunit;

namespace TheDeep.Tests;

public class CountdownTests
{
    [Fact]
    public void Starts_inactive()
    {
        Assert.False(new Countdown().Active);
    }

    [Fact]
    public void Stays_active_until_duration_elapses()
    {
        var timer = new Countdown();
        timer.Start(0.1f);
        timer.Tick(0.05f);
        Assert.True(timer.Active);
        timer.Tick(0.06f);
        Assert.False(timer.Active);
        Assert.Equal(0f, timer.Remaining);
    }

    [Fact]
    public void Clear_deactivates_immediately()
    {
        var timer = new Countdown();
        timer.Start(1f);
        timer.Clear();
        Assert.False(timer.Active);
    }

    [Fact]
    public void Coyote_window_covers_the_expected_number_of_ticks()
    {
        // 0.1 s at 60 Hz: refilled on the last grounded tick, then usable for the next 5 airborne ticks
        // (6 when float rounding leaves a sliver of time on the last one).
        const float dt = 1f / 60f;
        var coyote = new Countdown();
        coyote.Start(0.1f);
        int usableTicks = 0;
        for (int i = 0; i < 20; i++)
        {
            coyote.Tick(dt);
            if (coyote.Active)
            {
                usableTicks++;
            }
        }

        Assert.InRange(usableTicks, 5, 6);
    }
}
