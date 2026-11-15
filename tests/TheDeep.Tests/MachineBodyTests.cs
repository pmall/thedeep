using System;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>A machine's condition, crawl, gait and surface path (MachineVitals, CrawlMotion, MachineGait, LegGait, SurfacePath).</summary>
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
        Assert.Equal(new Vector2(1.5f, 1f), MachineVitals.Knockback(Locomotion.Walk, 2f, 1f, 3f, 0.5f, 2f));
        Assert.Equal(new Vector2(-3f, 1f), MachineVitals.Knockback(Locomotion.Hover, 0f, 1f, 3f, 1f, 1f));
        Assert.Null(MachineVitals.Knockback(Locomotion.Crawl, 0f, 1f, 3f, 1f, 2f));
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
    [InlineData(0f, 0f, 1)]         // 0 -> 0, plus 1
    [InlineData(10f, 2f, 145)]      // 13*10 + 7*2 = 144, plus 1
    [InlineData(-10f, 0f, 9844)]    // -130, below zero so + 9973 = 9843, plus 1
    public void TheSeedComesFromWhereAMachineStands(float x, float y, int seed)
    {
        Assert.Equal(seed, MachineGait.SeedAt(x, y));
    }

    [Fact]
    public void JitterIsFixedByItsSeedAndSaltAndStaysInUnitRange()
    {
        Assert.Equal(MachineGait.Jitter(145, 1), MachineGait.Jitter(145, 1));
        Assert.NotEqual(MachineGait.Jitter(145, 1), MachineGait.Jitter(145, 2));
        Assert.NotEqual(MachineGait.Jitter(145, 1), MachineGait.Jitter(146, 1));
        for (int seed = -50; seed < 9973; seed += 7)
        {
            float jitter = MachineGait.Jitter(seed, 1);
            Assert.InRange(jitter, 0f, 0.99999994f);
        }
    }

    [Fact]
    public void ClosedSurfacePathGoesAroundTheBox()
    {
        SurfacePath? path = SurfacePath.AroundBox(new Vector2(0f, 0f), new Vector2(2f, 4f), new[] { true, true, true, true }, 0.1f);
        Assert.NotNull(path);
        Assert.True(path.Closed);
        // Four faces plus four quarter circles of radius 0.1.
        Assert.Equal(12f + MathF.Tau * 0.1f, path.Length, 1);
        (Vector2 point, Vector2 normal) = path.At(path.Closest(new Vector2(3f, 2f)));
        Assert.Equal(new Vector2(2.1f, 2f), point);
        Assert.Equal(Vector2.UnitX, normal);
        Assert.Equal(path.At(0f).Point, path.At(path.Length).Point);
    }

    [Fact]
    public void OpenSurfacePathLeavesOutTheFaceOnTheFloor()
    {
        SurfacePath? path = SurfacePath.AroundBox(new Vector2(0f, 0f), new Vector2(2f, 4f), new[] { false, true, true, true }, 0f);
        Assert.NotNull(path);
        Assert.False(path.Closed);
        Assert.Equal(10f, path.Length, 2);
        // Up the right face, over the top, down the left face.
        Assert.Equal(new Vector2(2f, 0f), path.At(0f).Point);
        Assert.Equal(new Vector2(0f, 0f), path.At(path.Length).Point);
        Assert.Equal(new Vector2(0f, 1f), path.Tangent(1f));
        Assert.Equal(path.Length, path.Wrap(path.Length + 3f));
        Assert.True(path.AtEnd(path.Length, 1));
    }

    [Fact]
    public void PlantedFootStaysStillWhileTheBodyWalks()
    {
        const float Stride = 0.4f, Stance = 0.6f;
        float body = 0f, phase = 0.05f;
        float planted = body + LegGait.Foot(phase, Stance, Stride, 0.1f).X;
        for (int i = 0; i < 20; i++)
        {
            float step = 0.01f;
            body += step;
            phase += LegGait.Advance(step, Stride, Stance);
            Vector2 foot = LegGait.Foot(phase, Stance, Stride, 0.1f);
            Assert.Equal(0f, foot.Y);
            Assert.Equal(planted, body + foot.X, 4);
        }

        Assert.True(LegGait.Foot(Stance + (1f - Stance) * 0.5f, Stance, Stride, 0.1f).Y > 0.09f, "lifted mid-swing");
    }
}
