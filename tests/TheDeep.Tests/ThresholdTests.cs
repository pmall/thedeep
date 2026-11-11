using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class ThresholdTests
{
    private static readonly Vector2 Body = new(0.7f, 1.6f);

    // The Chapterhouse's west doorway (4 m tall, its marker on the floor 1 m inside the wall), a hatch 2 m wide in a floor, one in a roof.
    private static readonly Threshold West = new(ExitSide.West, new Vector2(1f, 0f), new Vector2(1f, 4f));
    private static readonly Threshold East = new(ExitSide.East, new Vector2(47f, 0f), new Vector2(1f, 4f));
    private static readonly Threshold Floor = new(ExitSide.Down, new Vector2(60f, -0.5f), new Vector2(2f, 1f));
    private static readonly Threshold Roof = new(ExitSide.Up, new Vector2(1f, 23.5f), new Vector2(2f, 1f));

    // The Chapterhouse's west end: the floor runs on under the wall, the vestibule over the doorway.
    private static readonly List<Box> WestEnd = new() { new(-3f, -4f, 30f, 0f), new(-3f, 4f, 0f, 26f), new(0f, 5f, 6f, 26f) };

    private static readonly Box Room = new(-1f, -2f, 80f, 26f);

    private static Box Standing(Vector2 feet) => new(feet.X - Body.X * 0.5f, feet.Y, feet.X + Body.X * 0.5f, feet.Y + Body.Y);

    [Fact]
    public void ADoorwaysTriggerCoversItsWholeOpening()
    {
        // Walking out, and jumping out through the top of the opening (the old trigger reached only half way up: the owner fell out).
        Assert.True(West.Crossed(Standing(new Vector2(0.8f, 0f))));
        Assert.True(West.Crossed(Standing(new Vector2(0.8f, 2.3f))));
        Assert.True(West.Crossed(Standing(new Vector2(-2f, 0f))));
        Assert.True(East.Crossed(Standing(new Vector2(47.2f, 1f))));
    }

    [Fact]
    public void StandingInADoorwayOrDroppingIntoItIsStillInTheRoom()
    {
        Assert.False(West.Crossed(Standing(West.Marker)));
        Assert.False(West.Crossed(Standing(new Vector2(0.9f, 3f))));
    }

    [Fact]
    public void AHatchIsCrossedByDippingIntoItsHole()
    {
        Assert.True(Floor.Crossed(Standing(new Vector2(60.9f, -0.05f))));
        Assert.False(Floor.Crossed(Standing(new Vector2(60f, 0f))));
        Assert.True(Roof.Crossed(Standing(new Vector2(0.2f, 21.5f))));
        Assert.False(Roof.Crossed(Standing(new Vector2(1f, 21.3f))));
    }

    [Fact]
    public void AnArrivalIsClearOfItsTrigger()
    {
        foreach (Threshold t in new[] { West, East, Floor, Roof, West with { Size = new Vector2(0.4f, 4f) } })
        {
            Assert.False(t.Crossed(Standing(t.Arrival(Body))), $"{t}");
        }

        Assert.Equal(West.Marker, West.Arrival(Body));
        Assert.Equal(new Vector2(60f, Threshold.Clearance), Floor.Arrival(Body));
        Assert.Equal(new Vector2(1f, 23f - Body.Y - Threshold.Clearance), Roof.Arrival(Body));
    }

    [Fact]
    public void AnArrivalNeverMovesBackOutAndClimbsClearOfAFloor()
    {
        Assert.Equal(new Vector2(0f, 3f), West.ArrivalVelocity(new Vector2(-6f, 3f), 9f));
        Assert.Equal(new Vector2(6f, 0f), West.ArrivalVelocity(new Vector2(6f, 0f), 9f));
        Assert.Equal(new Vector2(2f, 9f), Floor.ArrivalVelocity(new Vector2(2f, 3f), 9f));
        Assert.Equal(new Vector2(0f, 12f), Floor.ArrivalVelocity(new Vector2(0f, 12f), 9f));
        Assert.Equal(new Vector2(0f, -15f), Roof.ArrivalVelocity(new Vector2(0f, -15f), 9f));
    }

    [Fact]
    public void TheTwoEndsOfAWayFaceOppositeWays()
    {
        Assert.True(Threshold.Opposite(ExitSide.West, ExitSide.East));
        Assert.True(Threshold.Opposite(ExitSide.Up, ExitSide.Down));
        Assert.False(Threshold.Opposite(ExitSide.East, ExitSide.Down));
    }

    [Fact]
    public void ATriggerMustCoverItsWholeOpening()
    {
        Assert.Empty(West.Problems(WestEnd, Room, Body));

        // A marker mid-opening: nothing under it.
        Assert.Contains((West with { Marker = new Vector2(1f, 2f) }).Problems(WestEnd, Room, Body), p => p.Contains("not on the floor"));

        // An opening taller than its trigger by more than a body: a jump passes over it.
        var tall = new List<Box> { new(-3f, -4f, 30f, 0f), new(-3f, 7f, 6f, 26f) };
        Assert.Contains(West.Problems(tall, Room, Body), p => p.Contains("passes over it"));
        Assert.Empty((West with { Size = new Vector2(1f, 7f) }).Problems(tall, Room, Body));

        // A hole wider than its hatch.
        var floor = new List<Box> { new(40f, -2f, 59f, 0f), new(61f, -2f, 80f, 0f) };
        Assert.Empty(Floor.Problems(floor, Room, Body));
        var wide = new List<Box> { new(40f, -2f, 58f, 0f), new(61f, -2f, 80f, 0f) };
        Assert.Contains(Floor.Problems(wide, Room, Body), p => p.Contains("wider than the trigger on its west side"));
    }

    [Fact]
    public void AHatchArrivalMovesAcrossTheHoleToWhereTheBodyFits()
    {
        // The Coldstacks' vent: a 3 m hole, a ledge over its west half just above it. The arrival rises east of the ledge.
        var vent = new Threshold(ExitSide.Down, new Vector2(11.5f, -14f), new Vector2(3f, 1f));
        var ledge = new List<Box> { new(10f, -13f, 11.5f, -12.5f) };
        Vector2 feet = vent.Arrival(Body, ledge);
        Assert.Equal(12f, feet.X, 3);
        Assert.DoesNotContain(vent.Problems(ledge, Room, Body), p => p.Contains("arriving"));
    }

    [Fact]
    public void AFallDownAWideHatchKeepsItsLine()
    {
        // A chasm's 20 m opening: the body arrives as far from the middle as it crossed the other end, never past the hole's edge,
        // and moves across from there to where it fits.
        var sky = new Threshold(ExitSide.Up, new Vector2(36f, 27f), new Vector2(20f, 2f));
        Assert.Equal(28.5f, sky.Arrival(Body, along: -7.5f).X, 3);
        Assert.Equal(36f + 10f - Body.X * 0.5f - Threshold.Clearance, sky.Arrival(Body, along: 30f).X, 3);
        var rock = new List<Box> { new(27f, 20f, 30f, 26f) };
        Assert.Equal(30.5f, sky.Arrival(Body, rock, along: -7.5f).X, 3); // the first quarter-meter step clear of the rock
        // Rising through a floor, the climb out is steered from the middle.
        Assert.Equal(Floor.Marker.X, Floor.Arrival(Body, along: 0.6f).X, 3);
    }

    [Fact]
    public void AnArrivalMustFit()
    {
        var roof = new List<Box> { new(-5f, 23f, 0f, 26f), new(2f, 23f, 8f, 26f) };
        Assert.Empty(Roof.Problems(roof, Room, Body));
        Assert.Contains(Roof.Problems(roof.Append(new Box(-1f, 20f, 3f, 21.5f)).ToList(), Room, Body), p => p.Contains("arriving"));
    }
}
