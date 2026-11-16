using Xunit;

namespace TheDeep.Tests;

public class PilgrimControlTests
{
    private static readonly ControlLimits Limits = TestTuning.Control;

    private const float Tick = 1f / 60f;

    private static RawInput Held(float x = 0f, float y = 0f, bool jump = false, bool dash = false, bool harpoon = false) =>
        new(x, y, JumpHeld: jump, JumpPressed: jump, DashPressed: dash, HarpoonPressed: harpoon);

    [Theory]
    [InlineData(0.2f, 0)]
    [InlineData(-0.29f, 0)]
    [InlineData(0.3f, 1)]
    [InlineData(-0.8f, -1)]
    public void ASmallPushOfTheStickIsNeutral(float axis, int move)
    {
        var c = new PilgrimControl(Limits);
        Assert.Equal(move, c.Read(Held(x: axis, y: axis), landed: true).MoveX);
        Assert.Equal(move, c.Input.MoveY);
    }

    [Fact]
    public void AJumpPressedJustBeforeLandingStillJumps()
    {
        var c = new PilgrimControl(Limits);
        c.Read(Held(jump: true), landed: false);
        Assert.True(c.JumpPressed);
        Assert.False(c.WantsJump); // in the air, no coyote time

        for (int i = 0; i < 5; i++)
        {
            c.Tick(Tick);
            c.Read(Held(), landed: false);
        }

        c.Stood();
        Assert.True(c.WantsJump);
        c.ConsumeJump();
        Assert.False(c.WantsJump);
    }

    [Fact]
    public void APressKeptTooLongIsForgotten()
    {
        var c = new PilgrimControl(Limits);
        c.Read(Held(jump: true), landed: true);
        for (int i = 0; i < 12; i++)
        {
            c.Tick(Tick);
        }

        c.Stood();
        Assert.False(c.WantsJump);
    }

    [Fact]
    public void CoyoteTimeLetsAJumpLeaveJustAfterWalkingOffALedge()
    {
        var c = new PilgrimControl(Limits);
        c.Stood();
        for (int i = 0; i < 6; i++)
        {
            c.Tick(Tick); // 0.1 s off the ledge
        }

        c.Read(Held(jump: true), landed: false);
        Assert.True(c.WantsJump);

        for (int i = 0; i < 4; i++)
        {
            c.Tick(Tick);
        }

        Assert.False(c.WantsJump); // past 0.15 s
    }

    [Fact]
    public void TheAirDashIsSpentOffTheFloorAndBackOnLanding()
    {
        var c = new PilgrimControl(Limits);
        c.Read(Held(dash: true), landed: false);
        Assert.True(c.WantsDash(onFloor: false));
        c.BeginDash(onFloor: false);
        c.EndDash();
        Assert.Equal(0, c.AirDashesLeft);

        for (int i = 0; i < 30; i++)
        {
            c.Tick(Tick); // past the cooldown
        }

        c.Read(Held(dash: true), landed: false);
        Assert.False(c.WantsDash(onFloor: false));
        Assert.True(c.WantsDash(onFloor: true));
        c.Stood();
        Assert.Equal(1, c.AirDashesLeft);
        Assert.True(c.WantsDash(onFloor: false));
    }

    [Fact]
    public void TheDashCooldownHoldsTheNextDash()
    {
        var c = new PilgrimControl(Limits);
        c.Read(Held(dash: true), landed: true);
        c.BeginDash(onFloor: true);
        c.EndDash();
        c.Read(Held(dash: true), landed: true);
        Assert.False(c.WantsDash(onFloor: true));
        Assert.Equal(Limits.DashCooldown, c.DashCooldownRemaining, 3);
    }

    [Fact]
    public void ABlowTakesControlAndDropsThePressesKept()
    {
        var c = new PilgrimControl(Limits);
        c.Read(Held(jump: true, dash: true), landed: true);
        c.Hurt();
        Assert.True(c.Stunned);
        Assert.False(c.JumpPressed);
        Assert.Equal(default, c.Read(Held(x: 1f, jump: true), landed: true));
        Assert.False(c.JumpPressed); // nothing kept while reeling

        for (int i = 0; i < 14; i++)
        {
            c.Tick(Tick);
        }

        Assert.False(c.Stunned);
        Assert.Equal(1, c.Read(Held(x: 1f), landed: true).MoveX);
    }

    [Fact]
    public void SteeringHoldsADirectionUntilLanding()
    {
        var c = new PilgrimControl(Limits);
        c.Steer(-3, 1f);
        Assert.Equal(-1, c.Read(Held(x: 1f), landed: false).MoveX);
        Assert.Equal(1, c.Read(Held(x: 1f), landed: true).MoveX);
    }

    [Fact]
    public void LettingGoHoldsOffTheGripAMoment()
    {
        var c = new PilgrimControl(Limits);
        c.HoldOffGrip();
        Assert.True(c.GripHeldOff);
        for (int i = 0; i < 16; i++)
        {
            c.Tick(Tick);
        }

        Assert.False(c.GripHeldOff);
    }

    [Fact]
    public void AResetForgetsEverything()
    {
        var c = new PilgrimControl(Limits);
        c.Read(Held(jump: true, dash: true, harpoon: true), landed: false);
        c.BeginDash(onFloor: false);
        c.EndDash();
        c.Hurt();
        c.HoldOffGrip();
        c.Reset();
        Assert.False(c.GripHeldOff);
        Assert.False(c.JumpPressed);
        Assert.False(c.HarpoonPressed);
        Assert.False(c.Stunned);
        Assert.Equal(0f, c.DashCooldownRemaining);
        Assert.Equal(1, c.AirDashesLeft);
    }
}
