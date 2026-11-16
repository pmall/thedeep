using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class SteeringTests
{
    [Fact]
    public void ArrivingGoesAtFullSpeedFarOffThenSlowsToAStopOnTheTarget()
    {
        Assert.Equal(new Vector2(3f, 0f), Steering.Arrive(Vector2.Zero, new Vector2(10f, 0f), speed: 3f, slowRadius: 2f));
        Assert.Equal(new Vector2(0f, 1.5f), Steering.Arrive(Vector2.Zero, new Vector2(0f, 1f), speed: 3f, slowRadius: 2f));
        Assert.Equal(Vector2.Zero, Steering.Arrive(new Vector2(4f, 4f), new Vector2(4f, 4f), speed: 3f, slowRadius: 2f));
    }

    [Fact]
    public void AwaySideIsWhichSideOfTheThreatOneStandsOnAndTheFallbackOnATie()
    {
        Assert.Equal(1, Steering.AwaySide(new Vector2(5f, 0f), new Vector2(2f, 3f)));
        Assert.Equal(-1, Steering.AwaySide(new Vector2(1f, 0f), new Vector2(2f, 3f)));
        Assert.Equal(-1, Steering.AwaySide(new Vector2(2f, 0f), new Vector2(2f, 3f), fallback: -4));
        Assert.Equal(1, Steering.AwaySide(new Vector2(2f, 0f), new Vector2(2f, 3f)));
    }

    [Fact]
    public void LoiteringStaysWithinItsSizeOfHome()
    {
        var home = new Vector2(10f, 5f);
        for (float t = 0f; t < 60f; t += 0.5f)
        {
            Vector2 at = Steering.Loiter(home, t, size: 4f) - home;
            Assert.InRange(at.X, -2.001f, 2.001f);
            Assert.InRange(at.Y, -0.601f, 0.601f);
        }
    }
}
