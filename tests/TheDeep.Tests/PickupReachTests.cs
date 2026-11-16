using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>The proof's trigger box for a pickup (<see cref="PickupReach"/>).</summary>
public class PickupReachTests
{
    [Fact]
    public void TheBoxSpansTheRangeEachWayAroundTheRaisedPosition()
    {
        Box area = PickupReach.Area(new Vector2(10f, 4f), PickupReach.Take);
        Assert.Equal(10f - PickupReach.Take, area.MinX, 3);
        Assert.Equal(10f + PickupReach.Take, area.MaxX, 3);
        Assert.Equal(4f + PickupReach.Lift - PickupReach.Band * 0.5f, area.MinY, 3);
        Assert.Equal(4f + PickupReach.Lift + PickupReach.Band * 0.5f, area.MaxY, 3);
    }

    [Fact]
    public void ALongerRangeMakesAWiderBox()
    {
        Assert.True(PickupReach.Area(Vector2.Zero, 1.4f).MaxX > PickupReach.Area(Vector2.Zero, 1.2f).MaxX);
    }
}
