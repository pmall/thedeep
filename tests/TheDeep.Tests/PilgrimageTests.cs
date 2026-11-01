using Xunit;

namespace TheDeep.Tests;

public class PilgrimageTests
{
    [Fact]
    public void ANewRunStartsWhereTheFirstRoomIs()
    {
        var p = new Pilgrimage(new SaveData());
        p.Enter("Narthex");
        p.Enter("DrownedHall");
        Assert.Equal("Narthex", p.Save.Room); // until the first rest
        p.Rest("DrownedHall", "Shrine");
        p.Enter("Nave");
        Assert.Equal(("DrownedHall", "Shrine"), (p.Save.Room, p.Save.Shrine));
    }

    [Fact]
    public void WhatIsDoneStaysDone()
    {
        var p = new Pilgrimage(new SaveData());
        Assert.False(p.Done("Reliquary", "Relic"));
        Assert.True(p.Remember("Reliquary", "Relic"));
        Assert.False(p.Remember("Reliquary", "Relic"));
        Assert.True(p.Done("Reliquary", "Relic"));
        Assert.False(p.Done("Ossuary", "Relic"));
        Assert.Equal(new[] { "Reliquary/Relic" }, p.Save.Collected);
    }

    [Fact]
    public void ARelicLiesEmptyOnceTakenOrWhenItsAbilityIsOwned()
    {
        var p = new Pilgrimage(new SaveData());
        Assert.False(p.RelicEmpty("Reliquary", "Relic", Ability.MagGrip, Ability.Harpoon));
        Assert.True(p.RelicEmpty("Reliquary", "Relic", Ability.MagGrip, Ability.MagGrip | Ability.Drill));
        p.Remember("Reliquary", "Relic");
        Assert.True(p.RelicEmpty("Reliquary", "Relic", Ability.MagGrip, Ability.None));
    }

    [Fact]
    public void TheEndOfWhatIsBuiltIsShownOncePerVisit()
    {
        var p = new Pilgrimage(new SaveData());
        p.Enter("Brink");
        Assert.Equal(GoalMoment.Shown, p.ReachGoal(ending: false, bossLeft: false));
        Assert.Equal(GoalMoment.None, p.ReachGoal(ending: false, bossLeft: false));
        p.Enter("Brink");
        Assert.Equal(GoalMoment.Shown, p.ReachGoal(ending: false, bossLeft: false));
    }

    [Fact]
    public void TheEndOfTheGameWaitsForItsBoss()
    {
        var p = new Pilgrimage(new SaveData());
        p.Enter("Heart");
        Assert.Equal(GoalMoment.None, p.ReachGoal(ending: true, bossLeft: true));
        Assert.Equal(GoalMoment.End, p.ReachGoal(ending: true, bossLeft: false));
    }

    [Fact]
    public void TheSaveKeepsTheAbilitiesAndTheMap()
    {
        var p = new Pilgrimage(new SaveData());
        var map = new MapState();
        map.Enter("Narthex", System.Numerics.Vector2.Zero, new System.Numerics.Vector2(-2f, -2f), new System.Numerics.Vector2(38f, 22f));
        p.Record(Ability.MagGrip | Ability.Ballast, map);
        Assert.Equal((int)(Ability.MagGrip | Ability.Ballast), p.Save.Abilities);
        Assert.Single(p.Save.Map);
    }

    [Theory]
    [InlineData(-4.1f, true)]
    [InlineData(-3.9f, false)]
    public void FallingWellBelowTheRoomIsFallingOutOfIt(float y, bool fell)
    {
        Assert.Equal(fell, Pilgrimage.FellOut(y, floor: -1f, depth: 3f));
    }
}
