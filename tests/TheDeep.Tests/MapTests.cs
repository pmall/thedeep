using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class MapTests
{
    [Fact]
    public void DoorwayKeepsHeightButNotDepth()
    {
        // Leaving a doorway 1.2 m up, a little past its marker: arrives 1.2 m up on the arrival marker.
        Vector2 a = ExitMath.Arrival(new Vector2(1.4f, 1.2f), new Vector2(1f, 0f), new Vector2(63f, 26f), new Vector2(1f, 4f));
        Assert.Equal(63f, a.X, 3);
        Assert.Equal(27.2f, a.Y, 3);
    }

    [Fact]
    public void DoorwayNeverArrivesBelowItsMarker()
    {
        Vector2 a = ExitMath.Arrival(new Vector2(1f, -0.5f), new Vector2(1f, 0f), new Vector2(5f, 2f), new Vector2(1f, 4f));
        Assert.Equal(new Vector2(5f, 2f), a);
    }

    [Fact]
    public void HatchKeepsTheSideOffsetClampedInside()
    {
        Vector2 hatch = new(10f, 1f);
        Vector2 a = ExitMath.Arrival(new Vector2(53f, 31.5f), new Vector2(51f, 31f), new Vector2(20f, 0f), hatch);
        Assert.Equal(new Vector2(22f, 0f), a);
        Vector2 far = ExitMath.Arrival(new Vector2(70f, 31f), new Vector2(51f, 31f), new Vector2(20f, 0f), hatch);
        Assert.Equal(20f + 5f - ExitMath.EdgeMargin, far.X, 3);
    }

    [Fact]
    public void ClimbingThroughAHatchKeepsEnoughRise()
    {
        Vector2 hatch = new(6f, 1f);
        Assert.Equal(9f, ExitMath.ArrivalVelocity(new Vector2(2f, 3f), hatch, 9f).Y);
        Assert.Equal(12f, ExitMath.ArrivalVelocity(new Vector2(2f, 12f), hatch, 9f).Y);
        Assert.Equal(-15f, ExitMath.ArrivalVelocity(new Vector2(0f, -15f), hatch, 9f).Y);
        Assert.Equal(new Vector2(7f, 0f), ExitMath.ArrivalVelocity(new Vector2(7f, 0f), new Vector2(1f, 4f), 9f));
    }

    [Fact]
    public void DoorwaysLeadOutTowardTheNearerSide()
    {
        Vector2 min = new(-1f, -5f), max = new(57.5f, 26f), door = new(1f, 4f);
        Assert.Equal(-1, ExitMath.Outward(new Vector2(1f, 0f), door, min, max));
        Assert.Equal(1, ExitMath.Outward(new Vector2(57f, 14.5f), door, min, max));
        Assert.Equal(0, ExitMath.Outward(new Vector2(1f, 23.5f), new Vector2(2f, 1f), min, max));
    }

    [Fact]
    public void ADoorwayIsLeftOnlyByMovingOut()
    {
        // Falling into a west doorway from a flue above, or standing in it: stays.
        Assert.False(ExitMath.Leaves(-1, 0f, 0));
        Assert.False(ExitMath.Leaves(-1, 3f, 1));
        // Running, drifting or pressing west: leaves.
        Assert.True(ExitMath.Leaves(-1, -6f, 0));
        Assert.True(ExitMath.Leaves(-1, 0f, -1));
        // A hatch is always crossed.
        Assert.True(ExitMath.Leaves(0, 0f, 0));
    }

    [Fact]
    public void RoomsArePlacedFromTheExitsCrossed()
    {
        // East gate at x 63 of the first room leads to a west gate at x 1 of the next: that room starts 62 m east.
        Vector2 offset = ExitMath.RoomOffset(Vector2.Zero, new Vector2(63f, 26f), new Vector2(1f, 0f));
        Assert.Equal(new Vector2(62f, 26f), offset);
    }

    [Fact]
    public void MapKeepsTheFirstPlaceOfARoom()
    {
        var map = new MapState();
        map.Enter("A", Vector2.Zero, new Vector2(-2f, -2f), new Vector2(60f, 30f));
        map.Enter("B", new Vector2(62f, 0f), Vector2.Zero, new Vector2(40f, 20f));
        MapRoom again = map.Enter("A", new Vector2(5f, 5f), new Vector2(-2f, -2f), new Vector2(60f, 30f));

        Assert.Equal(Vector2.Zero, again.Offset);
        Assert.Equal("A", map.Current);
        Assert.Equal(2, map.Rooms.Count);
        Assert.Equal(new Vector2(102f, 20f), map.Find("B")!.MapMax);
    }
}
