using Xunit;

namespace TheDeep.Tests;

public class DialogueFlowTests
{
    /// <summary>20 letters a second, no pause anywhere.</summary>
    private static readonly RevealPace Even = new(20f, 0f, 0f);

    [Fact]
    public void ALineRevealsLetterByLetter()
    {
        var flow = new DialogueFlow(new[] { "Down where?" }, Even);

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
        var flow = new DialogueFlow(new[] { "You wake late.", "Down where?" }, Even);

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
        var flow = new DialogueFlow(new[] { "", "…" }, Even);

        Assert.True(flow.Whole);
        Assert.Equal(DialoguePress.Next, flow.Press());
    }

    [Fact]
    public void AConversationHasALine()
    {
        Assert.Throws<System.ArgumentException>(() => new DialogueFlow(System.Array.Empty<string>(), Even));
    }

    [Fact]
    public void ALinePausesAfterASentenceAndAComma()
    {
        var pace = new RevealPace(10f, 0.5f, 0.2f);
        var flow = new DialogueFlow(new[] { "Go. On, now" }, pace);

        flow.Tick(0.32f);
        Assert.Equal(3, flow.Shown); // "Go." then the sentence's pause before the space
        flow.Tick(0.45f);
        Assert.Equal(3, flow.Shown);
        flow.Tick(0.15f);
        Assert.Equal(4, flow.Shown);
        flow.Tick(0.3f);
        Assert.Equal(7, flow.Shown); // "Go. On," then the comma's shorter pause
        flow.Tick(0.15f);
        Assert.Equal(7, flow.Shown);
        flow.Tick(0.15f);
        Assert.Equal(8, flow.Shown);
    }

    [Fact]
    public void OnlyAMarkBeforeASpacePauses()
    {
        var pace = new RevealPace(10f, 1f, 1f);

        Assert.Equal(0.1f, pace.SecondsFor("3.5 m", 2), 4);
        Assert.Equal(0.1f, pace.SecondsFor("Wait...", 6), 4);
        Assert.Equal(1.1f, pace.SecondsFor("Wait... no", 7), 4);
        Assert.Equal(1.1f, pace.SecondsFor("Down? Yes", 5), 4);
        Assert.Equal(0.1f, pace.SecondsFor("Down? Yes", 0), 4);
    }

    [Fact]
    public void ALevelSpeakerCarriesItsPrompt()
    {
        var talk = new TalkPrompt(2f, 2.3f, 0.6f);

        (System.Numerics.Vector2 at, int reach) = talk.Place(new(10f, 1f), new(8f, 0f));

        Assert.Equal(new System.Numerics.Vector2(10f, 3.3f), at);
        Assert.Equal(0, reach);
    }

    [Fact]
    public void AFlyingSpeakersPromptStandsByThePilgrimLeaningTowardIt()
    {
        var talk = new TalkPrompt(2f, 2.3f, 0.6f);

        (System.Numerics.Vector2 at, int reach) = talk.Place(new(4f, 5.5f), new(8f, 0f));

        Assert.Equal(new System.Numerics.Vector2(7.4f, 2.3f), at);
        Assert.Equal(-1, reach);
        Assert.Equal(1, talk.Place(new(12f, -4f), new(8f, 0f)).Reach);
    }
}
