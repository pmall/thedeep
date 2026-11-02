using Xunit;

namespace TheDeep.Tests;

public class DialogueFlowTests
{
    [Fact]
    public void ALineRevealsLetterByLetter()
    {
        var flow = new DialogueFlow(new[] { "Down where?" }, 20f);

        Assert.Equal(0, flow.Shown);
        flow.Tick(0.25f);
        Assert.Equal(5, flow.Shown);
        Assert.False(flow.Whole);
        flow.Tick(10f);
        Assert.Equal(11, flow.Shown);
        Assert.True(flow.Whole);
    }

    [Fact]
    public void APressCompletesTheLineThenMovesOnThenEnds()
    {
        var flow = new DialogueFlow(new[] { "You wake late.", "Down where?" }, 20f);

        Assert.Equal(DialoguePress.Completed, flow.Press());
        Assert.True(flow.Whole);
        Assert.Equal(DialoguePress.Next, flow.Press());
        Assert.Equal(1, flow.Index);
        Assert.Equal(0, flow.Shown);
        flow.Tick(5f);
        Assert.Equal(DialoguePress.Ended, flow.Press());
        Assert.True(flow.Over);
        Assert.Equal(DialoguePress.Ended, flow.Press());
    }

    [Fact]
    public void ALineWithNoLettersIsWholeAtOnce()
    {
        var flow = new DialogueFlow(new[] { "", "…" }, 20f);

        Assert.True(flow.Whole);
        Assert.Equal(DialoguePress.Next, flow.Press());
    }

    [Fact]
    public void AConversationHasALine()
    {
        Assert.Throws<System.ArgumentException>(() => new DialogueFlow(System.Array.Empty<string>(), 20f));
    }
}
