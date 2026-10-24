using Xunit;

namespace TheDeep.Tests;

public class JumpMathTests
{
    [Theory]
    [InlineData(3.2f, 0.38f)]
    [InlineData(1f, 0.25f)]
    [InlineData(6f, 0.6f)]
    public void Derived_gravity_and_speed_reach_the_requested_apex(float height, float timeToApex)
    {
        float g = JumpMath.Gravity(height, timeToApex);
        float v0 = JumpMath.TakeOffSpeed(height, timeToApex);

        Assert.Equal(timeToApex, v0 / g, 4);              // time to apex: v0 / g
        Assert.Equal(height, v0 * v0 / (2f * g), 3);      // apex height: v0² / 2g
    }

    [Fact]
    public void Simulated_jump_at_60hz_peaks_near_the_requested_height()
    {
        const float dt = 1f / 60f;
        float g = JumpMath.Gravity(3.2f, 0.38f);
        float vy = JumpMath.TakeOffSpeed(3.2f, 0.38f);
        float y = 0f, peak = 0f;
        while (vy > 0f)
        {
            vy -= g * dt;
            y += vy * dt;
            peak = System.MathF.Max(peak, y);
        }

        Assert.InRange(peak, 3.0f, 3.25f);
    }
}
