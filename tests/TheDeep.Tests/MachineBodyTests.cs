using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>A machine's condition, crawl and gait (MachineVitals, CrawlMotion, MachineGait).</summary>
public class MachineBodyTests
{
    private const float Tick = 1f / 60f;

    /// <summary>A brain that always walks right, alert and roused.</summary>
    private sealed class Walker : MachineBrain
    {
        public override Intent Think(in Perception p, float delta) => new() { Velocity = new Vector2(2f, 0f), Rouse = 1f, Alert = 1f };
    }

    private static readonly Perception Alone = Perception.Alone(0f, Vector2.Zero, 1);

    [Fact]
    public void ABlowStaggersTheBrainAndFlashes()
    {
        var v = new MachineVitals(health: 3, staggerTime: 0.2f);
        var brain = new Walker();
        Intent last = v.Think(brain, Alone, default, Tick);
        Assert.Equal(2f, last.Velocity.X);

        Assert.False(v.Hit(1));
        Assert.Equal(1f, v.Flash);
        Intent reeling = v.Think(brain, Alone, last, Tick);
        Assert.Equal(Vector2.Zero, reeling.Velocity); // stunned: nothing wanted...
        Assert.Equal(1f, reeling.Rouse); // ...but still roused
        Assert.True(v.Flash < 1f);

        for (int i = 0; i < 12; i++)
        {
            v.Think(brain, Alone, reeling, Tick);
        }

        Assert.Equal(2f, v.Think(brain, Alone, reeling, Tick).Velocity.X);
    }

    [Fact]
    public void ABossShrugsHitsOff()
    {
        var v = new MachineVitals(health: 10, staggerTime: 0f);
        v.Hit(1);
        Assert.Equal(2f, v.Think(new Walker(), Alone, default, Tick).Velocity.X);
    }

    [Fact]
    public void TheLastBlowBreaksItAndItFoldsAway()
    {
        var v = new MachineVitals(health: 2, staggerTime: 0.35f);
        Assert.False(v.Hit(1));
        Assert.True(v.Hit(1));
        Assert.True(v.Broken);
        Assert.False(v.Hit(1)); // broken once only
        Assert.Equal(0f, v.Folded);

        v.Fold(MachineVitals.BreakTime * 0.5f);
        Assert.Equal(0.5f, v.Folded, 4);
        Assert.False(v.Gone);
        v.Fold(MachineVitals.BreakTime);
        Assert.Equal(1f, v.Folded);
        Assert.True(v.Gone);
    }

    [Fact]
    public void ABossBeatenBeforeIsGoneAtOnce()
    {
        var v = new MachineVitals(health: 5, staggerTime: 0f);
        v.Vanish();
        Assert.True(v.Broken);
        Assert.True(v.Gone);
    }

    [Fact]
    public void ABlowPushesAwayByHeftButNeverACrawler()
    {
        Assert.Equal(new Vector2(1.5f, 1f), MachineVitals.Knockback(Locomotion.Walk, 2f, 1f, 3f, 0.5f));
        Assert.Equal(new Vector2(-3f, 1f), MachineVitals.Knockback(Locomotion.Hover, 0f, 1f, 3f, 1f));
        Assert.Null(MachineVitals.Knockback(Locomotion.Crawl, 0f, 1f, 3f, 1f));
    }

    [Fact]
    public void ACrawlerGoesRoundItsBlockAndStopsAtTheEndsOfAnOpenPath()
    {
        SurfacePath? round = SurfacePath.AroundBox(new Vector2(0f, 0f), new Vector2(2f, 4f), new[] { true, true, true, true }, 0.1f);
        Assert.NotNull(round);
        var crawl = new CrawlMotion(round, new Vector2(1f, 4.1f));
        float moved = 0f;
        for (int i = 0; i < 600; i++)
        {
            moved += crawl.Step(crawl.Tangent * 5f, maxSpeed: 2f, acceleration: 20f, Tick);
        }

        Assert.True(moved > round.Length, $"moved {moved} round a path of {round.Length}"); // more than once round, never jumping back

        SurfacePath? open = SurfacePath.AroundBox(new Vector2(0f, 0f), new Vector2(2f, 4f), new[] { false, true, true, true }, 0f);
        Assert.NotNull(open);
        var stuck = new CrawlMotion(open, new Vector2(1f, 4f));
        for (int i = 0; i < 600; i++)
        {
            stuck.Step(stuck.Tangent * 5f, maxSpeed: 2f, acceleration: 20f, Tick);
        }

        Assert.True(open.AtEnd(stuck.S, 1));
        Assert.Equal(0f, stuck.Step(stuck.Tangent * 5f, maxSpeed: 2f, acceleration: 20f, Tick), 3);
    }

    [Fact]
    public void AMachineFacesAsAskedElseTheWayItMoves()
    {
        Assert.Equal(-1, MachineGait.Facing(asked: -1, travelled: 0.5f, forward: 1, current: 1));
        Assert.Equal(-1, MachineGait.Facing(asked: 0, travelled: -0.5f, forward: 1, current: 1));
        Assert.Equal(1, MachineGait.Facing(asked: 0, travelled: -0.5f, forward: -1, current: -1)); // a crawler upside down
        Assert.Equal(-1, MachineGait.Facing(asked: 0, travelled: 0f, forward: 1, current: -1));
    }

    [Fact]
    public void AFrozenTickMovesNothing()
    {
        var gait = new MachineGait();
        Assert.Equal(0f, gait.Moved(0f, 0f));
        Assert.Equal(0f, gait.Stepping);
    }

    [Fact]
    public void SteppingEasesInWhileWalkingAndOutWhenStanding()
    {
        var gait = new MachineGait();
        for (int i = 0; i < 60; i++)
        {
            gait.Moved(0.02f, Tick);
        }

        Assert.True(gait.Stepping > 0.99f);
        Assert.Equal(1.2f, gait.Walked, 3);
        for (int i = 0; i < 60; i++)
        {
            gait.Moved(0f, Tick);
        }

        Assert.True(gait.Stepping < 0.01f);
    }

    [Fact]
    public void AWalkerFallsUntilItStandsAgain()
    {
        Assert.Equal(-MachineGait.FloorPress, MachineGait.Fall(-5f, onFloor: true, Tick));
        Assert.Equal(-MachineGait.Gravity * Tick, MachineGait.Fall(0f, onFloor: false, Tick), 4);
        Assert.Equal(-MachineGait.MaxFallSpeed, MachineGait.Fall(-MachineGait.MaxFallSpeed, onFloor: false, Tick));
    }

    [Theory]
    [InlineData(0f, 0f, 1)]
    [InlineData(10f, 2f, 145)]
    [InlineData(-10f, 0f, 9844)]
    public void TheSeedComesFromWhereAMachineStands(float x, float y, int seed)
    {
        Assert.Equal(seed, MachineGait.SeedAt(x, y));
    }
}
