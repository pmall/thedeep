using Xunit;

namespace TheDeep.Tests;

public class FrameTimeStatsTests
{
    [Fact]
    public void Empty_stats_are_zero()
    {
        var stats = new FrameTimeStats();
        Assert.Equal(0, stats.Count);
        Assert.Equal(0.0, stats.Percentile(99));
    }

    [Fact]
    public void Tracks_average_worst_and_percentiles()
    {
        var stats = new FrameTimeStats();
        for (int i = 1; i <= 100; i++)
        {
            stats.Add(i);
        }

        Assert.Equal(50.5, stats.Average, 9);
        Assert.Equal(100.0, stats.Worst);
        Assert.Equal(99.0, stats.Percentile(99));
        Assert.Equal(50.0, stats.Percentile(50));
        Assert.Equal(1.0, stats.Percentile(0));
        Assert.Equal(10, stats.CountOver(90));
    }
}
