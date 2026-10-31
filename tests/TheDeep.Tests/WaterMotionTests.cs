using System;
using Xunit;

namespace TheDeep.Tests;

public class WaterMotionTests
{
    // The live tuning (resources/player_tuning.tres, the Water group).
    public static readonly WaterLimits Water = new(SwimSpeed: 4.5f, SwimAcceleration: 30f, SwimDeceleration: 18f, FloatDepth: 1.05f,
        FloatStiffness: 4f, RiseSpeed: 4f, SinkSpeed: 4.5f, WaterAcceleration: 22f, SplashKeep: 0.3f, EnterDepth: 0.55f, JumpWindow: 0.4f);

    private const float Dt = 1f / 60f;

    /// <summary>Feet from <paramref name="feet"/> with <paramref name="vy"/> for <paramref name="seconds"/>: the lowest and the last height.</summary>
    private static (float Lowest, float Last) Run(float feet, float vy, bool sunk, float seconds)
    {
        float lowest = feet;
        for (int i = 0; i < seconds / Dt; i++)
        {
            vy = WaterMotion.Vertical(vy, feet, 0f, sunk, Water, Dt);
            feet += vy * Dt;
            lowest = MathF.Min(lowest, feet);
        }

        return (lowest, feet);
    }

    [Fact]
    public void APlungeDipsAndBobsBackToTheFloatLine()
    {
        // A fall at the top speed (20 m/s) breaks the surface; the feet are 0.55 m deep when it starts to swim.
        float vy = WaterMotion.Splash(-20f, Water);
        Assert.Equal(-6f, vy, 3);
        (float lowest, float last) = Run(-0.55f, vy, sunk: false, seconds: 3f);
        float line = WaterMotion.FloatLine(0f, Water);
        Assert.InRange(last, line - 0.05f, line + 0.05f);
        Assert.True(lowest > line - 0.8f, $"dipped to {lowest}, too deep under the float line {line}");
    }

    [Fact]
    public void FloatingRisesFromTheDeep()
    {
        (_, float last) = Run(-10f, 0f, sunk: false, seconds: 5f);
        Assert.InRange(last, WaterMotion.FloatLine(0f, Water) - 0.05f, WaterMotion.FloatLine(0f, Water) + 0.05f);
    }

    [Fact]
    public void SunkSinksAtSinkSpeed()
    {
        float vy = 0f;
        for (int i = 0; i < 60; i++)
        {
            vy = WaterMotion.Vertical(vy, -2f, 0f, sunk: true, Water, Dt);
        }

        Assert.Equal(-Water.SinkSpeed, vy, 3);
    }

    [Fact]
    public void SwimmingReachesSwimSpeed()
    {
        float vx = 7f; // ran in at run speed
        for (int i = 0; i < 60; i++)
        {
            vx = WaterMotion.Horizontal(vx, 1, Water, Dt);
        }

        Assert.Equal(Water.SwimSpeed, vx, 3);
    }

    [Theory]
    [InlineData(-1.05f, true)]  // floating
    [InlineData(-0.6f, true)]   // wading, above the float line
    [InlineData(-1.4f, true)]   // just inside the window
    [InlineData(-2f, false)]    // sunk
    public void JumpingOut(float feet, bool can) => Assert.Equal(can, WaterMotion.CanJumpOut(feet, 0f, Water));
}
