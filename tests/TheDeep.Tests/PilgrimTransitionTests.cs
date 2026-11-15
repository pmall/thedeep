using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class PilgrimTransitionTests
{
    [Fact]
    public void ADiveNeedsTheDrillDownHeldUprightAndDryOrSunk()
    {
        Assert.True(DashRules.AsksForDive(true, -1, true, false, false));
        Assert.True(DashRules.AsksForDive(true, -1, true, true, true));
        Assert.False(DashRules.AsksForDive(true, -1, true, true, false)); // in water without the tanks full
        Assert.False(DashRules.AsksForDive(false, -1, true, false, false));
        Assert.False(DashRules.AsksForDive(true, 0, true, false, false));
        Assert.False(DashRules.AsksForDive(true, -1, false, false, false));
    }

    [Fact]
    public void ADiveStartsFromTheAirOrFromCrackedRock()
    {
        Assert.True(DashRules.CanDiveFrom(onFloor: false, crackedBelow: false));
        Assert.True(DashRules.CanDiveFrom(onFloor: true, crackedBelow: true));
        Assert.False(DashRules.CanDiveFrom(onFloor: true, crackedBelow: false));
    }

    [Fact]
    public void WithoutBallastTheTanksStayEmpty()
    {
        Assert.Equal(new BallastStep(false, false), BallastTanks.Step(false, true, -1, true));
    }

    [Fact]
    public void DownFillsTheTanksAndUpOrJumpBlowsThemSpendingTheJump()
    {
        Assert.Equal(new BallastStep(true, false), BallastTanks.Step(true, false, -1, false));
        Assert.Equal(new BallastStep(false, true), BallastTanks.Step(true, true, 1, false));
        Assert.Equal(new BallastStep(false, true), BallastTanks.Step(true, true, 0, true));
        Assert.Equal(new BallastStep(true, false), BallastTanks.Step(true, true, 0, false));
        Assert.Equal(new BallastStep(false, false), BallastTanks.Step(true, false, 1, true)); // empty: nothing to blow, the jump stays
    }

    [Fact]
    public void AGripSpeedAcceleratesWithItsWayAndBrakesAgainstIt()
    {
        Assert.Equal(10f, GripMath.SpeedRate(3f, 0f, 10f, 20f));
        Assert.Equal(10f, GripMath.SpeedRate(3f, 2f, 10f, 20f));
        Assert.Equal(20f, GripMath.SpeedRate(3f, -2f, 10f, 20f));
        Assert.Equal(20f, GripMath.SpeedRate(0f, 2f, 10f, 20f));
    }

    [Fact]
    public void ALockedGripInputKeepsItsWayUntilTheInputChanges()
    {
        var steer = new GripSteer();
        Vector2 up = new(1f, 0f); // on a wall facing right
        Vector2 held = new(1f, 0f); // pulling away from it
        Assert.Null(steer.Along(held, up, 0.5f));

        steer.Lock(held, 1f);
        Assert.Equal(1f, steer.Along(held, up, 0.5f));
        Assert.Equal(0f, steer.Along(Vector2.Zero, up, 0.5f)); // changed: unlocked, no pull
        Assert.Null(steer.Along(held, up, 0.5f)); // the lock is gone

        steer.Lock(held, 1f);
        steer.Clear();
        Assert.Null(steer.Along(held, up, 0.5f));
    }

    [Fact]
    public void AGripAlongTheSurfaceIsTheInputsComponentAlongIt()
    {
        Assert.Equal(-1f, GripSteer_Along(new Vector2(0f, 1f), new Vector2(1f, 0f)), 3);
    }

    private static float GripSteer_Along(Vector2 input, Vector2 up) => new GripSteer().Along(input, up, 0.5f) ?? float.NaN;

    [Fact]
    public void APullGathersSpeedTowardTheAnchor()
    {
        PullStep step = HarpoonPull.Step(new Vector2(10f, 0f), 0f, 20f, 100f, 0.1f);
        Assert.Equal(10f, step.Speed, 3);
        Assert.False(step.Arriving);
        Assert.Equal(10f, step.Velocity.X, 3);
        Assert.Equal(0f, step.Velocity.Y, 3);
    }

    [Fact]
    public void AnAnchorWithinATicksTravelIsLandedOnExactly()
    {
        PullStep step = HarpoonPull.Step(new Vector2(0f, 0.5f), 20f, 20f, 100f, 0.1f);
        Assert.True(step.Arriving);
        Assert.Equal(5f, step.Velocity.Y, 3); // 0.5 m in 0.1 s
    }

    [Fact]
    public void ABiteKeepsOnlyTheSpeedAlongTheLine()
    {
        Vector2 dir = HarpoonPull.Direction(new Vector2(0f, 4f));
        Assert.Equal(3f, HarpoonPull.BiteSpeed(new Vector2(5f, 3f), dir), 3);
        Assert.Equal(0f, HarpoonPull.BiteSpeed(new Vector2(0f, -3f), dir));
        Assert.Equal(new Vector2(0f, 1f), HarpoonPull.Direction(Vector2.Zero));
    }

    [Fact]
    public void APullBlockedForSeveralTicksInARowGivesUp()
    {
        int blocked = 0;
        for (int i = 0; i < HarpoonPull.StuckTicks - 1; i++)
        {
            blocked = HarpoonPull.Blocked(0f, 1f, blocked);
            Assert.False(HarpoonPull.GivesUp(blocked));
        }

        Assert.True(HarpoonPull.GivesUp(HarpoonPull.Blocked(0f, 1f, blocked)));
        Assert.Equal(0, HarpoonPull.Blocked(1f, 1f, blocked)); // moving again resets the count
    }

    [Fact]
    public void AnAnchorsLookFollowsTheHarpoonsState()
    {
        Assert.Equal(AnchorLook.Dormant, Look(armed: false, fired: true));
        Assert.Equal(AnchorLook.Sighted, Look(fired: true));
        Assert.Equal(AnchorLook.Bitten, Look(fired: true, bitten: true));
        Assert.Equal(AnchorLook.Sighted, Look(target: true));
        Assert.Equal(AnchorLook.Spent, Look(lastZipped: true));
        Assert.Equal(AnchorLook.Ready, Look(distance: 5f));
        Assert.Equal(AnchorLook.Far, Look(distance: 12f));
        Assert.Equal(AnchorLook.Far, Look(distance: 0.5f));
    }

    private static AnchorLook Look(bool armed = true, bool fired = false, bool bitten = false, bool target = false, bool lastZipped = false, float distance = 5f) =>
        AnchorLooks.Of(armed, fired, bitten, target, lastZipped, distance, range: 9f, minDistance: 1.2f);

    [Fact]
    public void AShakeFadesAndGrowsWithItsSquare()
    {
        var shake = new CameraShake();
        shake.Add(1f);
        shake.Tick(0.1f, 2f);
        Assert.Equal(0.8f, shake.Trauma, 3);
        shake.Tick(10f, 2f);
        Assert.Equal(0f, shake.Trauma);
        Assert.Equal(Vector2.Zero, shake.Offset(0.12f));

        var full = new CameraShake();
        var half = new CameraShake();
        full.Add(1f);
        half.Add(0.5f);
        full.Tick(0.1f, 0f);
        half.Tick(0.1f, 0f);
        Vector2 big = full.Offset(0.12f);
        Assert.NotEqual(Vector2.Zero, big);
        Assert.Equal(big.X * 0.25f, half.Offset(0.12f).X, 4);
        Assert.Equal(big.Y * 0.25f, half.Offset(0.12f).Y, 4);
    }

    [Fact]
    public void ALandingShakesOnlyAboveItsThresholdAndHardestIsFull()
    {
        var shake = new CameraShake();
        shake.AddLanding(13f, 13f, 22f);
        Assert.Equal(0f, shake.Trauma);
        shake.AddLanding(13.1f, 13f, 22f);
        Assert.Equal(CameraShake.LandingTrauma * CameraShake.LandingFloor, shake.Trauma, 3);
        shake.AddLanding(30f, 13f, 22f);
        Assert.Equal(CameraShake.LandingTrauma * (1f + CameraShake.LandingFloor), shake.Trauma, 3);
    }

    [Fact]
    public void AShakeOffsetStaysWithinItsAmplitude()
    {
        var shake = new CameraShake();
        shake.Add(1f);
        for (int i = 0; i < 200; i++)
        {
            shake.Tick(0.01f, 0f);
            Vector2 o = shake.Offset(0.12f);
            Assert.InRange(o.X, -0.06f, 0.06f);
            Assert.InRange(o.Y, -0.12f, 0.12f);
        }
    }
}
