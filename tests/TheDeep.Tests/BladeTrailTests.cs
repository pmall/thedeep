using System;
using System.Numerics;
using TheDeep;
using Xunit;

namespace TheDeep.Tests;

public class BladeTrailTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void ASweepBetweenTwoTicksIsFilledAlongItsArcAroundThePivot()
    {
        var trail = new BladeTrail(0.2f, 6, 64, 10f);
        trail.Step(Dt, Vector3.Zero, new Vector3(0f, 0.3f, 0f), new Vector3(0f, 1f, 0f), 1f);
        trail.Step(Dt, Vector3.Zero, new Vector3(0.3f, 0f, 0f), new Vector3(1f, 0f, 0f), 1f); // a quarter turn in one tick
        Assert.Equal(6, trail.Count);
        for (int i = 0; i < trail.Count; i++)
        {
            Assert.InRange(trail[i].Tip.Length(), 0.99f, 1.01f); // on the circle, not cut across it
        }

        Assert.True(trail[trail.Count - 1].Life > trail[0].Life, "the newest brightest");
    }

    [Fact]
    public void ItFadesAndEmptiesOnceTheBladeStopsLayingIt()
    {
        var trail = new BladeTrail(0.1f, 4, 64, 10f);
        trail.Step(Dt, Vector3.Zero, Vector3.UnitY * 0.3f, Vector3.UnitY, 1f);
        trail.Step(Dt, Vector3.Zero, Vector3.UnitX * 0.3f, Vector3.UnitX, 1f);
        int laid = trail.Count;
        trail.Step(Dt, Vector3.Zero, -Vector3.UnitY * 0.3f, -Vector3.UnitY, 0f); // the strength easing to zero still closes the arc
        Assert.True(trail.Count > laid);
        for (int i = 0; i < 10; i++)
        {
            trail.Step(Dt, Vector3.Zero, -Vector3.UnitY * 0.3f, -Vector3.UnitY, 0f);
        }

        Assert.Equal(0, trail.Count);
    }

    [Fact]
    public void ABladeNearlyStillLaysAlmostNothing()
    {
        var trail = new BladeTrail(0.2f, 4, 64, 10f);
        trail.Step(Dt, Vector3.Zero, Vector3.UnitY * 0.3f, Vector3.UnitY, 1f);
        trail.Step(Dt, Vector3.Zero, Vector3.UnitY * 0.3f, new Vector3(0.01f, 1f, 0f), 1f); // 0.6 m/s
        Assert.True(trail[trail.Count - 1].Life < 0.1f);
    }

    [Fact]
    public void ItKeepsNoMoreThanItsCapacityDroppingTheOldest()
    {
        var trail = new BladeTrail(10f, 8, 20, 10f);
        for (int i = 0; i < 10; i++)
        {
            float a = i * 0.3f;
            trail.Step(Dt, Vector3.Zero, Vector3.Zero, new Vector3(MathF.Cos(a), MathF.Sin(a), 0f), 1f);
        }

        Assert.Equal(20, trail.Count);
        Assert.InRange(MathF.Atan2(trail[trail.Count - 1].Tip.Y, trail[trail.Count - 1].Tip.X), 2.69f, 2.71f);
    }
}
