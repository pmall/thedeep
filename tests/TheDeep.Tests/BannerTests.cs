using Xunit;

namespace TheDeep.Tests;

public class BannerTests
{
    [Fact]
    public void ALongLineIsHeldLongerThanAShortOne()
    {
        Assert.Equal(5f, ReadingTime.Seconds(80, 16f, 2.5f), 3);
        Assert.Equal(10f, ReadingTime.Seconds(160, 16f, 2.5f), 3);
    }

    [Fact]
    public void AShortLineIsHeldAGlanceAtLeast()
    {
        Assert.Equal(2.5f, ReadingTime.Seconds(8, 16f, 2.5f), 3);
        Assert.Equal(2.5f, ReadingTime.Seconds(0, 16f, 2.5f), 3);
    }

    [Fact]
    public void WordsAreReadAtSomePace() =>
        Assert.Throws<System.ArgumentOutOfRangeException>(() => ReadingTime.Seconds(10, 0f, 1f));

    [Fact]
    public void TheBannerStandsHighWhileThePilgrimIsBelowIt()
    {
        Assert.False(BannerPlace.Low(pilgrimTop: 500f, highBottom: 260f, clearance: 48f, low: false));
        Assert.False(BannerPlace.Low(pilgrimTop: 261f, highBottom: 260f, clearance: 48f, low: false));
    }

    [Fact]
    public void ThePilgrimClimbingIntoTheHighPlaceSendsItLow() =>
        Assert.True(BannerPlace.Low(pilgrimTop: 240f, highBottom: 260f, clearance: 48f, low: false));

    [Fact]
    public void ItComesBackHighOnlyOnceThePilgrimIsClearByTheMargin()
    {
        Assert.True(BannerPlace.Low(pilgrimTop: 280f, highBottom: 260f, clearance: 48f, low: true));
        Assert.False(BannerPlace.Low(pilgrimTop: 320f, highBottom: 260f, clearance: 48f, low: true));
    }
}
