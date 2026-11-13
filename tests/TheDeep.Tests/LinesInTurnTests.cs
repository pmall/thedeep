using Xunit;

namespace TheDeep.Tests;

public class LinesInTurnTests
{
    [Fact]
    public void ALineOfferedWhileNothingShowsShowsAtOnce()
    {
        var queue = new LinesInTurn<string>(2);
        Assert.True(queue.Offer("heat"));
        Assert.True(queue.Showing);
        Assert.Equal("heat", queue.Current);
    }

    [Fact]
    public void TwoLinesAtOnceAreReadInTurn()
    {
        var queue = new LinesInTurn<string>(2);
        queue.Offer("seal");
        Assert.False(queue.Offer("heat"));
        Assert.Equal("seal", queue.Current);
        Assert.True(queue.Next(out string next));
        Assert.Equal("heat", next);
        Assert.False(queue.Next(out _));
        Assert.False(queue.Showing);
    }

    [Fact]
    public void ALineShowingOrWaitingIsNotQueuedAgain()
    {
        var queue = new LinesInTurn<string>(3);
        queue.Offer("seal");
        queue.Offer("heat");
        Assert.False(queue.Offer("seal"));
        Assert.False(queue.Offer("heat"));
        Assert.Equal(1, queue.Waiting);
    }

    [Fact]
    public void TheQueueNeverBacksUpPastItsCap()
    {
        var queue = new LinesInTurn<int>(2);
        for (int i = 0; i < 6; i++)
        {
            queue.Offer(i);
        }

        Assert.Equal(2, queue.Waiting);
        queue.Next(out int a);
        queue.Next(out int b);
        Assert.Equal((1, 2), (a, b));
    }

    [Fact]
    public void ClearingDropsAllAndTheNextLineShowsAtOnce()
    {
        var queue = new LinesInTurn<string>(2);
        queue.Offer("seal");
        queue.Offer("heat");
        queue.Clear();
        Assert.False(queue.Showing);
        Assert.True(queue.Offer("metal"));
    }

    [Fact]
    public void ALineWithdrawnWhileShowingLeavesAndTheNextShows()
    {
        var queue = new LinesInTurn<string>(2);
        queue.Offer("refusal");
        queue.Offer("heat");
        Assert.True(queue.Withdraw("refusal"));
        Assert.Equal("heat", queue.Current);
        Assert.Equal(0, queue.Waiting);
    }

    [Fact]
    public void ALineWithdrawnWhileWaitingIsNeverRead()
    {
        var queue = new LinesInTurn<string>(2);
        queue.Offer("seal");
        queue.Offer("refusal");
        Assert.False(queue.Withdraw("refusal"));
        Assert.Equal("seal", queue.Current);
        Assert.False(queue.Next(out _));
    }

    [Fact]
    public void ALineNeverSaidWithdrawsNothing()
    {
        var queue = new LinesInTurn<string>(2);
        queue.Offer("seal");
        Assert.False(queue.Withdraw("refusal"));
        Assert.Equal("seal", queue.Current);
    }
}
