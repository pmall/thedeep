using Xunit;

namespace TheDeep.Tests;

public class ArenaFightTests
{
    [Fact]
    public void ItBeginsWhenThePilgrimStepsInAndEndsWhenNoFoeStands()
    {
        var fight = new ArenaFight();

        Assert.Equal(ArenaChange.None, fight.Step(pilgrimIn: false, foesStand: true));
        Assert.Equal(ArenaChange.Began, fight.Step(pilgrimIn: true, foesStand: true));
        Assert.Equal(ArenaChange.None, fight.Step(pilgrimIn: false, foesStand: true));
        Assert.Equal(ArenaChange.Cleared, fight.Step(pilgrimIn: true, foesStand: false));
        Assert.Equal(ArenaPhase.Won, fight.Phase);
        Assert.Equal(ArenaChange.None, fight.Step(pilgrimIn: true, foesStand: true));
    }

    [Fact]
    public void WithNoFoeItNeverBegins()
    {
        var fight = new ArenaFight();

        Assert.Equal(ArenaChange.None, fight.Step(pilgrimIn: true, foesStand: false));
        Assert.Equal(ArenaPhase.Waiting, fight.Phase);
    }

    [Fact]
    public void SettledItStaysWon()
    {
        var fight = new ArenaFight();
        fight.Settle();

        Assert.Equal(ArenaChange.None, fight.Step(pilgrimIn: true, foesStand: true));
        Assert.Equal(ArenaPhase.Won, fight.Phase);
    }
}
