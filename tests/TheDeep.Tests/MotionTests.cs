using Xunit;

namespace TheDeep.Tests;

public class MotionTests
{
    [Theory]
    [InlineData(0f, 10f, 3f, 3f)]
    [InlineData(9f, 10f, 3f, 10f)]
    [InlineData(10f, 0f, 4f, 6f)]
    [InlineData(1f, 0f, 4f, 0f)]
    [InlineData(5f, 5f, 1f, 5f)]
    public void ApproachMovesByAtMostMaxDeltaWithoutOvershooting(float current, float target, float maxDelta, float expected)
    {
        Assert.Equal(expected, Motion.Approach(current, target, maxDelta), 5);
    }

    [Fact]
    public void DampIsFrameRateIndependent()
    {
        float oneStep = Motion.Damp(0f, 10f, 5f, 0.1f);
        float twoSteps = Motion.Damp(Motion.Damp(0f, 10f, 5f, 0.05f), 10f, 5f, 0.05f);
        Assert.Equal(oneStep, twoSteps, 4);
    }

    [Fact]
    public void DampConvergesTowardTarget()
    {
        float value = 0f;
        for (int i = 0; i < 600; i++)
        {
            value = Motion.Damp(value, 10f, 5f, 1f / 60f);
        }

        Assert.Equal(10f, value, 3);
    }
}
