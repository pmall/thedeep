using Xunit;

namespace TheDeep.Tests;

public class PromptArbiterTests
{
    [Fact]
    public void HigherPrioritySlotWinsAndFallsBackWhenCleared()
    {
        var arbiter = new PromptArbiter();
        object shrine = new(), plate = new(), law = new(), lever = new(), talk = new();

        arbiter.Set(InteractionPriority.Conversation, talk, 0.5f);
        Assert.True(arbiter.IsActive(talk));

        arbiter.Set(InteractionPriority.Lever, lever, 1.0f);
        Assert.True(arbiter.IsActive(lever));
        Assert.False(arbiter.IsActive(talk));

        arbiter.Set(InteractionPriority.LawBreak, law, 1.0f);
        Assert.True(arbiter.IsActive(law));

        arbiter.Set(InteractionPriority.Plate, plate, 1.0f);
        Assert.True(arbiter.IsActive(plate));

        arbiter.Set(InteractionPriority.Shrine, shrine, 1.5f);
        Assert.True(arbiter.IsActive(shrine));
        Assert.True(arbiter.Any);

        arbiter.Set(InteractionPriority.Shrine, null);
        Assert.True(arbiter.IsActive(plate));

        arbiter.Set(InteractionPriority.Plate, null);
        Assert.True(arbiter.IsActive(law));

        arbiter.Set(InteractionPriority.LawBreak, null);
        Assert.True(arbiter.IsActive(lever));

        arbiter.Set(InteractionPriority.Lever, null);
        Assert.True(arbiter.IsActive(talk));

        arbiter.Set(InteractionPriority.Conversation, null);
        Assert.False(arbiter.Any);
        Assert.Null(arbiter.Winner);
    }

    [Fact]
    public void TryUseConsumesPressOncePerTickEvenAfterWinnerClears()
    {
        var arbiter = new PromptArbiter();
        object lever = new(), talk = new();

        arbiter.Set(InteractionPriority.Lever, lever, 1.0f);
        arbiter.Set(InteractionPriority.Conversation, talk, 0.8f);

        Assert.False(arbiter.TryUse(talk, tick: 42));
        Assert.True(arbiter.TryUse(lever, tick: 42));

        // Lever is now pulled and leaves the arbiter, promoting talk on the same tick.
        arbiter.Set(InteractionPriority.Lever, null);
        Assert.True(arbiter.IsActive(talk));
        Assert.False(arbiter.TryUse(talk, tick: 42));

        // Next tick accepts a new press.
        Assert.True(arbiter.TryUse(talk, tick: 43));
    }

    [Fact]
    public void ClearRemovesAllSlotsAndResetConsumedTick()
    {
        var arbiter = new PromptArbiter();
        object shrine = new();

        arbiter.Set(InteractionPriority.Shrine, shrine, 0.4f);
        Assert.True(arbiter.TryUse(shrine, tick: 10));

        arbiter.Clear();
        Assert.False(arbiter.Any);
        Assert.Null(arbiter.Winner);

        arbiter.Set(InteractionPriority.Shrine, shrine, 0.4f);
        Assert.True(arbiter.TryUse(shrine, tick: 10));
    }
}
