using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class MachinesTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void SeesOnlyWhatIsInFrontAndInSight()
    {
        var p = With(Vector2.Zero, 1, new PlayerTrace(new Vector2(3f, 1f), Vector2.Zero, true));
        Assert.True(Senses.SeesPlayer(p, 5f));
        Assert.False(Senses.SeesPlayer(p with { Facing = -1 }, 5f));
        Assert.True(Senses.SeesPlayer(p with { Facing = -1 }, 5f, behind: 4f));
        Assert.False(Senses.SeesPlayer(p with { Player = new PlayerTrace(new Vector2(3f, 1f), Vector2.Zero, false) }, 5f));
        Assert.False(Senses.SeesPlayer(p, 2f));
    }

    [Fact]
    public void HearsALoudImpactNearbyButNotFootstepsOrFarAway()
    {
        var p = Perception.Alone(0f, Vector2.Zero, 1);
        Assert.NotNull(Senses.Hears(p with { Impacts = new[] { new Impact(new Vector2(3f, 0f), 18f) } }, 10f, 7f));
        Assert.Null(Senses.Hears(p with { Impacts = new[] { new Impact(new Vector2(3f, 0f), 5f) } }, 10f, 7f));
        Assert.Null(Senses.Hears(p with { Impacts = new[] { new Impact(new Vector2(9.5f, 0f), 20f) } }, 10f, 7f));
    }

    [Fact]
    public void PrayersFallIntoStep()
    {
        var a = new PrayerCycle(3f, 0f) { Coupling = 0.12f };
        var b = new PrayerCycle(3.3f, 0.45f) { Coupling = 0.12f };
        for (int i = 0; i < 60 * 60; i++)
        {
            float pa = a.Phase, pb = b.Phase;
            a.Step(Dt, new[] { pb });
            b.Step(Dt, new[] { pa });
        }

        float gap = MathF.Abs(a.Phase - b.Phase);
        Assert.True(MathF.Min(gap, 1f - gap) < 0.05f, $"phases {a.Phase} and {b.Phase}");
    }

    [Fact]
    public void BowGoesDownHoldsAndRises()
    {
        Assert.Equal(0f, PrayerCycle.BowAt(0f), 3);
        Assert.Equal(1f, PrayerCycle.BowAt(0.4f), 3);
        Assert.Equal(0f, PrayerCycle.BowAt(0.9f), 3);
        Assert.InRange(PrayerCycle.BowAt(0.65f), 0.2f, 0.8f);
    }

    [Fact]
    public void StrikeWindsUpBeforeTheBlowAndCoolsDown()
    {
        var strike = new StrikeAction { WindUp = 0.4f, Blow = 0.1f, Recover = 0.3f, Cooldown = 0.5f };
        Assert.True(strike.Start());
        Assert.False(strike.Start());
        int hits = 0, hitTick = -1;
        for (int i = 0; i < 60; i++)
        {
            if (strike.Update(Dt))
            {
                hits++;
                hitTick = i;
            }
        }

        Assert.Equal(1, hits);
        Assert.InRange(hitTick * Dt, 0.48f, 0.52f);
        Assert.False(strike.Active);
        Assert.False(strike.Ready);
        for (int i = 0; i < 40; i++)
        {
            strike.Update(Dt);
        }

        Assert.True(strike.Ready);
    }

    [Fact]
    public void PrayerStrikesWhoPassesInFrontButNotBehind()
    {
        var brain = NewPrayer();
        var behind = With(Vector2.Zero, 1, new PlayerTrace(new Vector2(-1f, 1f), Vector2.Zero, true));
        Assert.DoesNotContain(Run(brain, behind, 120), i => i.Hit);
        Assert.Equal(MachineAct.Pray, brain.Act);

        var inFront = behind with { Player = new PlayerTrace(new Vector2(1.2f, 1f), Vector2.Zero, true) };
        List<Intent> ticks = Run(brain, inFront, 60);
        Assert.Contains(ticks, i => i.Hit);
        Assert.True(ticks.First().Act == MachineAct.WindUp, "the wind-up warns first");
        Assert.True(ticks.Last().Alert > 0.8f);

        // Once the intruder has gone, it calms down and prays again.
        Run(brain, behind with { Player = null }, 240);
        Assert.Equal(MachineAct.Pray, brain.Act);
    }

    [Fact]
    public void PrayingNeighborsBowTogether()
    {
        var a = NewPrayer(phase: 0f);
        var b = NewPrayer(phase: 0.5f);
        Vector2 pa = Vector2.Zero, pb = new(2.5f, 0f);
        Intent ia = default, ib = default;
        for (int i = 0; i < 60 * 90; i++)
        {
            float sa = ia.Signal, sb = ib.Signal;
            ia = a.Think(Perception.Alone(i * Dt, pa, 1) with { Neighbors = new[] { new Neighbor(pb, "votary", sb) } }, Dt);
            ib = b.Think(Perception.Alone(i * Dt, pb, 1) with { Neighbors = new[] { new Neighbor(pa, "votary", sa) } }, Dt);
        }

        Assert.True(MathF.Abs(ia.Pray - ib.Pray) < 0.1f, $"bows {ia.Pray} and {ib.Pray}");
    }

    [Fact]
    public void HoarderFleesFromThePlayerAndCowersWhenCornered()
    {
        var brain = new HoarderBrain(1);
        Vector2 position = new(5f, 0f);
        var player = new PlayerTrace(new Vector2(2f, 1f), Vector2.Zero, true);
        Intent intent = brain.Think(With(position, 1, player), Dt);
        Assert.Equal(MachineAct.Flee, brain.Act);
        Assert.True(intent.Velocity.X > 5f, "runs away, to the right");

        intent = brain.Think(With(position, 1, player) with { BlockedRight = true }, Dt);
        Assert.Equal(MachineAct.Cower, brain.Act);
        Assert.Equal(Vector2.Zero, intent.Velocity);
        Assert.Equal(-1, intent.Facing);
    }

    [Fact]
    public void HoarderGleansAroundItsHomeWithoutThePlayer()
    {
        var brain = new HoarderBrain(7) { HomeRange = 3f };
        var position = new Vector2(10f, 0f);
        var acts = new HashSet<MachineAct>();
        for (int i = 0; i < 60 * 30; i++)
        {
            Intent intent = brain.Think(Perception.Alone(i * Dt, position, 1), Dt);
            position += intent.Velocity * Dt;
            acts.Add(brain.Act);
            Assert.InRange(position.X, 6.5f, 13.5f);
        }

        Assert.Contains(MachineAct.Glean, acts);
        Assert.Contains(MachineAct.Wander, acts);
        Assert.DoesNotContain(MachineAct.Flee, acts);
    }

    [Fact]
    public void FollowerTrailsThePlayerAtADistanceAndNeverStrikes()
    {
        var brain = new FollowerBrain { Distance = 4f, Height = 1.5f };
        var position = new Vector2(0f, 3f);
        var player = new Vector2(3f, 1f);
        for (int i = 0; i < 60 * 12; i++)
        {
            player.X += 3f * Dt; // walking right
            var trace = new PlayerTrace(player, new Vector2(3f, 0f), true);
            Intent intent = brain.Think(With(position, 1, trace), Dt);
            position += intent.Velocity * Dt;
            Assert.False(intent.Hit);
        }

        Assert.Equal(MachineAct.Follow, brain.Act);
        Assert.InRange(player.X - position.X, 3f, 5.5f);
        Assert.InRange(position.Y - player.Y, 1f, 2f);
    }

    [Fact]
    public void FollowerDartsAwayWhenApproached()
    {
        var brain = new FollowerBrain { Shy = 2f };
        var player = new PlayerTrace(new Vector2(0f, 1f), Vector2.Zero, true);
        brain.Think(With(new Vector2(-4f, 2.5f), 1, player), Dt);
        Intent intent = brain.Think(With(new Vector2(-1f, 1.5f), 1, player), Dt);
        Assert.Equal(MachineAct.Startle, brain.Act);
        Assert.True(intent.Velocity.X < 0f);
    }

    [Fact]
    public void SleeperWakesOnAHardLandingHuntsAndSleepsAgain()
    {
        var brain = new SleeperBrain(new StrikeAction()) { Patience = 2f, WakeTime = 0.5f };
        var position = new Vector2(0f, 2f);
        var quiet = Perception.Alone(0f, position, 1);
        Run(brain, quiet with { Impacts = new[] { new Impact(new Vector2(2f, 0f), 4f) } }, 1);
        Assert.Equal(MachineAct.Sleep, brain.Act);

        Run(brain, quiet with { Impacts = new[] { new Impact(new Vector2(3f, 0f), 18f) } }, 1);
        Assert.Equal(MachineAct.Wake, brain.Act);
        List<Intent> hunting = Run(brain, quiet, 45);
        Assert.Equal(MachineAct.Hunt, brain.Act);
        Assert.True(hunting.Last().Velocity.X > 0f, "toward the noise");

        Run(brain, quiet, 60 * 3);
        Assert.Equal(MachineAct.Sleep, brain.Act);
    }

    [Fact]
    public void SleeperStrikesWhenThePlayerIsInReach()
    {
        var brain = new SleeperBrain(new StrikeAction()) { WakeTime = 0.2f };
        var player = new PlayerTrace(new Vector2(1f, 0.5f), Vector2.Zero, true);
        var p = With(Vector2.Zero, 1, player);
        brain.Think(p with { Impacts = new[] { new Impact(new Vector2(1f, 0f), 20f) } }, Dt);
        Assert.Contains(Run(brain, p, 90), i => i.Hit);
    }

    [Fact]
    public void ClosedSurfacePathGoesAroundTheBox()
    {
        SurfacePath path = SurfacePath.AroundBox(new Vector2(0f, 0f), new Vector2(2f, 4f), new[] { true, true, true, true }, 0.1f)!;
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
        SurfacePath path = SurfacePath.AroundBox(new Vector2(0f, 0f), new Vector2(2f, 4f), new[] { false, true, true, true }, 0f)!;
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

    private static PrayerBrain NewPrayer(float phase = 0f) =>
        new("votary", 3f, phase, new StrikeAction()) { Reach = 2f, Calm = 1f };

    private static Perception With(Vector2 position, int facing, PlayerTrace player) =>
        Perception.Alone(0f, position, facing) with { Player = player };

    private static List<Intent> Run(MachineBrain brain, Perception p, int ticks)
    {
        var intents = new List<Intent>();
        for (int i = 0; i < ticks; i++)
        {
            intents.Add(brain.Think(p with { Time = p.Time + i * Dt }, Dt));
        }

        return intents;
    }
}
