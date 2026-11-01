using Xunit;

namespace TheDeep.Tests;

public class MoveRulesTests
{
    private static readonly ControlLimits L = PilgrimControlTests.Limits;

    [Fact]
    public void RunningReachesRunSpeedAndNoFurther()
    {
        float vx = 0f;
        for (int i = 0; i < 60; i++)
        {
            vx = MoveRules.Run(vx, 1, grounded: true, L, 1f / 60f);
        }

        Assert.Equal(L.RunSpeed, vx, 4);
        Assert.Equal(L.GroundAcceleration / 60f, MoveRules.Run(0f, 1, grounded: true, L, 1f / 60f), 4);
        Assert.Equal(L.AirAcceleration / 60f, MoveRules.Run(0f, 1, grounded: false, L, 1f / 60f), 4);
    }

    [Fact]
    public void LettingGoOrTurningBrakes()
    {
        Assert.Equal(L.RunSpeed - L.GroundDeceleration / 60f, MoveRules.Run(L.RunSpeed, 0, grounded: true, L, 1f / 60f), 4);
        Assert.Equal(L.RunSpeed - L.AirDeceleration / 60f, MoveRules.Run(L.RunSpeed, -1, grounded: false, L, 1f / 60f), 4);
        // Faster than a run (a dash's exit), pushing on: it slows to a run.
        Assert.True(MoveRules.Run(12f, 1, grounded: true, L, 1f / 60f) < 12f);
    }

    [Fact]
    public void GravityHangsAtTheApexWhileJumpIsHeldAndPullsHarderFalling()
    {
        const float dt = 1f / 60f;
        Assert.Equal(1f - L.Gravity * L.ApexGravityMultiplier * dt, MoveRules.Fall(1f, jumpHeld: true, onFloor: false, L, dt), 4);
        Assert.Equal(1f - L.Gravity * dt, MoveRules.Fall(1f, jumpHeld: false, onFloor: false, L, dt), 4);
        Assert.Equal(-5f - L.Gravity * L.FallGravityMultiplier * dt, MoveRules.Fall(-5f, jumpHeld: false, onFloor: false, L, dt), 4);
        Assert.Equal(-L.MaxFallSpeed, MoveRules.Fall(-L.MaxFallSpeed, jumpHeld: false, onFloor: false, L, dt));
    }

    [Fact]
    public void ABlowThrowsAwayFromItAndUp()
    {
        Assert.Equal(new System.Numerics.Vector2(L.HurtKnockback, L.HurtLift), MoveRules.Knockback(2f, 1f, L));
        Assert.Equal(new System.Numerics.Vector2(-L.HurtKnockback, L.HurtLift), MoveRules.Knockback(0f, 1f, L));
    }

    [Fact]
    public void TheLedgeAssistKeepsTheSpeedOrAtLeastMostOfARun()
    {
        Assert.Equal(0.6f * 7f, LedgeAssist.Speed(1f, 7f), 4);
        Assert.Equal(9f, LedgeAssist.Speed(-9f, 7f), 4);
    }
}
