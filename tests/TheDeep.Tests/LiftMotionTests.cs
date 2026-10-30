using Xunit;

namespace TheDeep.Tests;

public class LiftMotionTests
{
    private const float Dt = 1f / 60f;

    private static LiftMotion Lift() => new(rise: 20f, speed: 4f, acceleration: 2f, boardDelay: 0.5f, returnSpeed: 1.5f, returnDelay: 2f);

    private static int Run(LiftMotion lift, bool ridden, float seconds, System.Func<LiftMotion, bool>? until = null)
    {
        int released = 0;
        for (int i = 0; i < (int)(seconds / Dt); i++)
        {
            if (lift.Step(Dt, ridden))
            {
                released++;
            }

            if (until?.Invoke(lift) == true)
            {
                break;
            }
        }

        return released;
    }

    [Fact]
    public void WaitsUntilRiddenThenLetsGoAfterTheDelay()
    {
        var lift = Lift();
        Run(lift, ridden: false, 3f);
        Assert.Equal(LiftPhase.Waiting, lift.Phase);
        Run(lift, ridden: true, 0.4f);
        Assert.Equal(LiftPhase.Boarding, lift.Phase);
        Assert.Equal(0f, lift.Offset);
        Assert.Equal(1, Run(lift, ridden: true, 0.2f));
        Assert.Equal(LiftPhase.Rising, lift.Phase);
    }

    [Fact]
    public void SteppingOffWhileBoardingKeepsItWaiting()
    {
        var lift = Lift();
        Run(lift, ridden: true, 0.3f);
        Run(lift, ridden: false, 0.1f);
        Assert.Equal(LiftPhase.Waiting, lift.Phase);
        Run(lift, ridden: true, 0.4f);
        Assert.Equal(LiftPhase.Boarding, lift.Phase); // the delay starts over
    }

    [Fact]
    public void RisesSmoothlyAndArrivesAtRest()
    {
        var lift = Lift();
        Run(lift, ridden: true, 0.6f);
        float last = lift.Offset, top = 0f, lastSpeed = lift.Velocity, worstJerk = 0f;
        for (int i = 0; i < 60 * 30 && lift.Phase == LiftPhase.Rising; i++)
        {
            lift.Step(Dt, ridden: true);
            Assert.True(lift.Offset >= last, "it never sinks while rising");
            Assert.True(lift.Offset <= lift.Rise);
            if (lift.Phase == LiftPhase.Rising)
            {
                worstJerk = System.MathF.Max(worstJerk, System.MathF.Abs(lift.Velocity - lastSpeed));
            }

            top = System.MathF.Max(top, lift.Velocity);
            last = lift.Offset;
            lastSpeed = lift.Velocity;
        }

        Assert.Equal(LiftPhase.Arrived, lift.Phase);
        Assert.Equal(20f, lift.Offset);
        Assert.Equal(0f, lift.Velocity);
        Assert.Equal(4f, top, 2);
        Assert.True(worstJerk <= 2f * Dt + 0.05f, $"speed changes gently ({worstJerk} per tick)");
    }

    [Fact]
    public void OnceOnItsWayARiderSteppingOffDoesNotStopIt()
    {
        var lift = Lift();
        Run(lift, ridden: true, 1f);
        Run(lift, ridden: false, 30f, l => l.Phase == LiftPhase.Arrived);
        Assert.Equal(LiftPhase.Arrived, lift.Phase);
        Assert.Equal(20f, lift.Offset);
    }

    [Fact]
    public void EmptyAtTheTopItSinksBackSlowlyToWaitAgain()
    {
        var lift = Lift();
        Run(lift, ridden: true, 30f, l => l.Phase == LiftPhase.Arrived);
        Run(lift, ridden: true, 5f);
        Assert.Equal(LiftPhase.Arrived, lift.Phase); // a rider at the top keeps it there
        Run(lift, ridden: false, 2.1f);
        Assert.Equal(LiftPhase.Returning, lift.Phase);
        Run(lift, ridden: false, 1f);
        Assert.True(lift.Velocity < 0f && lift.Velocity >= -1.5f);
        Run(lift, ridden: false, 60f, l => l.Phase == LiftPhase.Waiting);
        Assert.Equal(LiftPhase.Waiting, lift.Phase);
        Assert.Equal(0f, lift.Offset);
    }
}
