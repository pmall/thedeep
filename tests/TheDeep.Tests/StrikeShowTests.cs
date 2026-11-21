using Xunit;

namespace TheDeep.Tests;

public class StrikeShowTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void TheBladeIsOutByTheCoilAndTheSlashOnlyThroughTheCut()
    {
        var show = new StrikeShow();
        show.Strike(0.1f, 0.15f, 0.5f, StrikeKind.Swing);
        show.Update(Dt);
        Assert.Equal(0f, show.Edge);
        Assert.True(show.Blade > 0.5f);

        show.Strike(0.3f, 0.15f, 0.5f, StrikeKind.Swing);
        show.Update(Dt);
        Assert.True(show.Edge > 0.9f);
        Assert.True(show.Blade > 0.99f);
        Assert.Equal(0.3f, show.Progress);
    }

    [Fact]
    public void TheEndPoseHoldsThenLetsGoAndTheBladeFolds()
    {
        var show = new StrikeShow();
        show.Strike(0.9f, 0.15f, 0.5f, StrikeKind.Finisher);
        show.Update(Dt);
        show.End();
        show.Update(Dt);
        Assert.Equal(1f, show.Weight);
        Assert.Equal(1f, show.Progress);
        for (int i = 0; i < 60; i++)
        {
            show.Update(Dt);
        }

        Assert.Equal(0f, show.Weight);
        Assert.Equal(0f, show.Blade);
    }

    [Fact]
    public void AResetHidesEverythingAtOnce()
    {
        var show = new StrikeShow();
        show.Strike(0.3f, 0.15f, 0.5f, StrikeKind.Dash);
        show.Update(Dt);
        show.Reset();
        Assert.Equal(0f, show.Blade);
        Assert.Equal(0f, show.Weight);
        Assert.Equal(0f, show.Edge);
    }
}
