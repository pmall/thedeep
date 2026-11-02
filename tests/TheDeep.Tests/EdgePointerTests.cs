using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class EdgePointerTests
{
    private static readonly Vector2 Screen = new(1920f, 1080f);

    [Fact]
    public void OnScreenNeedsNoMark()
    {
        Assert.Null(EdgePointer.For(new Vector2(900f, 400f), Screen, 40f));
    }

    [Fact]
    public void NearTheEdgeIsPointedAt()
    {
        Assert.NotNull(EdgePointer.For(new Vector2(1900f, 540f), Screen, 40f));
    }

    [Fact]
    public void AboveIsHeldOnTheTopEdgePointingUp()
    {
        EdgePointer.Mark mark = EdgePointer.For(new Vector2(960f, -300f), Screen, 40f)?? throw new Xunit.Sdk.XunitException("no mark");
        Assert.Equal(new Vector2(960f, 40f), mark.Position);
        Assert.Equal(new Vector2(0f, -1f), mark.Direction);
    }

    [Fact]
    public void DiagonalStaysOnTheLineFromTheCenterAndInside()
    {
        var target = new Vector2(2600f, -900f);
        EdgePointer.Mark mark = EdgePointer.For(target, Screen, 40f)?? throw new Xunit.Sdk.XunitException("no mark");
        Assert.InRange(mark.Position.X, 40f, 1880.01f);
        Assert.InRange(mark.Position.Y, 39.99f, 1040f);
        Vector2 a = Vector2.Normalize(mark.Position - Screen * 0.5f), b = Vector2.Normalize(target - Screen * 0.5f);
        Assert.True(Vector2.Dot(a, b) > 0.9999f);
        Assert.True(Vector2.Dot(mark.Direction, b) > 0.9999f);
    }
}
