using Xunit;

namespace TheDeep.Tests;

public class HoldProgressTests
{
    private static HoldProgress Offered(float seconds = 1f)
    {
        var hold = new HoldProgress(seconds);
        hold.Step(held: false, offered: true, 0.016f);
        return hold;
    }

    [Fact]
    public void FillsWhileHeldAndCompletesOnce()
    {
        HoldProgress hold = Offered();
        Assert.False(hold.Step(true, true, 0.5f));
        Assert.Equal(0.5f, hold.Fraction, 3);
        Assert.True(hold.Step(true, true, 0.5f));
        Assert.False(hold.Step(true, true, 0.5f));
    }

    [Fact]
    public void LettingGoDropsToZeroAtOnce()
    {
        HoldProgress hold = Offered();
        hold.Step(true, true, 0.9f);
        hold.Step(false, true, 0.01f);
        Assert.Equal(0f, hold.Fraction);
    }

    [Fact]
    public void TapsNeverAddUp()
    {
        HoldProgress hold = Offered();
        for (int i = 0; i < 100; i++)
        {
            Assert.False(hold.Step(true, true, 0.1f * 0.9f));
            Assert.False(hold.Step(false, true, 0.1f));
        }

        Assert.Equal(0f, hold.Fraction);
    }

    [Fact]
    public void TheOfferGoingDropsTheHold()
    {
        HoldProgress hold = Offered();
        hold.Step(true, true, 0.9f);
        Assert.False(hold.Step(true, false, 0.1f));
        Assert.Equal(0f, hold.Fraction);
    }

    [Fact]
    public void AButtonHeldBeforeTheOfferCountsNothingUntilLetGo()
    {
        var hold = new HoldProgress(1f);
        hold.Step(true, false, 0.1f);
        Assert.False(hold.Step(true, true, 2f));
        Assert.Equal(0f, hold.Fraction);
        hold.Step(false, true, 0.1f);
        Assert.True(hold.Step(true, true, 1f));
    }

    [Fact]
    public void ANewHoldNeedsANewPress()
    {
        HoldProgress hold = Offered();
        Assert.True(hold.Step(true, true, 1f));
        Assert.False(hold.Step(true, true, 5f));
        hold.Step(false, true, 0.1f);
        Assert.True(hold.Step(true, true, 1f));
    }

    [Fact]
    public void AHoldTakesSomeTime()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new HoldProgress(0f));
    }
}
