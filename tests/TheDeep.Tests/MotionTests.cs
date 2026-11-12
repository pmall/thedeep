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

    [Fact]
    public void SmoothDampSettlesWithoutOvershootAndIsFrameRateIndependent()
    {
        float a = 0f, va = 0f, b = 0f, vb = 0f;
        for (int i = 0; i < 120; i++)
        {
            a = Motion.SmoothDamp(a, 10f, ref va, 0.2f, 1f / 60f);
            Assert.True(a <= 10f + 1e-4f);
        }

        for (int i = 0; i < 240; i++)
        {
            b = Motion.SmoothDamp(b, 10f, ref vb, 0.2f, 1f / 120f);
        }

        Assert.Equal(10f, a, 2);
        Assert.Equal(a, b, 2);
    }
}
