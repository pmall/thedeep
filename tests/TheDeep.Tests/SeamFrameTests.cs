using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class SeamFrameTests
{
    // The common doorway: 4 m tall, a stretch of floor 1 m deep before it.
    private static readonly SeamFrame Door = new(4f, 1f);

    // The common hatch: a hole 3 m wide through a slab 2 m thick.
    private static readonly SeamFrame Hatch = new(3f, 2f);

    private static readonly Box Wide = new(-200f, -200f, 200f, 200f);

    private static SeamRoom Room(Box bounds, IEnumerable<Box> solids, string exit, Threshold at) =>
        new(bounds, solids.ToList(), new List<(string, Box)>(), new List<LiftPath>(), new Dictionary<string, Threshold> { [exit] = at });

    [Fact]
    public void ADoorwaysExitIsAsTallAsItsOpening()
    {
        Assert.Equal(new Vector2(1f, 4f), Door.ExitSize(ExitSide.East));
        Assert.Equal(new Vector2(4f, 1f), Door.ExitSize(ExitSide.Up));
    }

    [Fact]
    public void ADoorwaysFrameIsASillAndALintelRunningThroughTheWholeSeam()
    {
        IReadOnlyList<Box> frame = Door.Solids(ExitSide.East, new Vector2(37f, 0f));
        float reach = SeamCheck.Around + 0.5f;
        Assert.Equal(new[] { new Box(37f - reach, -SeamCheck.Around, 37f + reach, 0f), new Box(37f - reach, 4f, 37f + reach, 4f + SeamCheck.Around) }, frame);
        Assert.Empty(Door.Exit(ExitSide.East, new Vector2(37f, 0f)).Problems(frame, Wide, new Vector2(0.7f, 1.6f)));
    }

    [Fact]
    public void AHatchsFrameIsTheSlabEitherSideOfTheHole()
    {
        IReadOnlyList<Box> frame = Hatch.Solids(ExitSide.Down, new Vector2(10f, -1f));
        Assert.Equal(new[] { new Box(8.5f - SeamCheck.Around, -2f, 8.5f, 0f), new Box(11.5f, -2f, 11.5f + SeamCheck.Around, 0f) }, frame);
        Assert.Equal(new Vector2(3f, 2f), Hatch.ExitSize(ExitSide.Up));
    }

    [Fact]
    public void TwoRoomsFramingAHatchFromOneDefinitionAgree()
    {
        // The upper room bounds its camera at the slab's underside, the lower one at its top; each room's floor or ceiling stops at the frame.
        Threshold down = Hatch.Exit(ExitSide.Down, new Vector2(60f, -1f)), up = Hatch.Exit(ExitSide.Up, new Vector2(20f, 29.5f));
        var upper = Hatch.Solids(ExitSide.Down, down.Marker).Append(new Box(-3f, -2f, 56.5f, 0f)).Append(new Box(63.5f, -2f, 68f, 0f));
        var lower = Hatch.Solids(ExitSide.Up, up.Marker).Append(new Box(-3f, 28.5f, 16.5f, 33f)).Append(new Box(17.5f, 2.5f, 18.5f, 28.5f));
        var check = new SeamCheck();
        check.AddRoom("Nave", Room(new Box(-1f, -2f, 86.5f, 24f), upper, "Hatch", down));
        check.AddRoom("Cistern", Room(new Box(-1.5f, -2f, 41f, 30.5f), lower, "Top", up));
        check.AddWay("Nave", "Hatch", "Cistern", "Top");

        Assert.Empty(check.Problems(1.6f));
    }

    [Fact]
    public void TwoRoomsFramingAWayFromOneDefinitionAgree()
    {
        // The west room's own east wall stops at the frame; the east room's walls overhang where the west room's camera can't see.
        Threshold east = Door.Exit(ExitSide.East, new Vector2(37f, 0f)), west = Door.Exit(ExitSide.West, new Vector2(1f, 0f));
        var westRoom = Door.Solids(ExitSide.East, east.Marker).Append(new Box(-4f, -2f, 34.5f, 0f)).Append(new Box(36f, 6f, 40f, 20f));
        var eastRoom = Door.Solids(ExitSide.West, west.Marker).Append(new Box(3.5f, -2f, 66f, 0f)).Append(new Box(-2f, 6f, 0f, 34f));
        var check = new SeamCheck();
        check.AddRoom("Narthex", Room(new Box(-2f, -2f, 38f, 22f), westRoom, "East", east));
        check.AddRoom("DrownedHall", Room(new Box(-2f, -2f, 66f, 34f), eastRoom, "WestGate", west));
        check.AddWay("Narthex", "East", "DrownedHall", "WestGate");

        Assert.Empty(check.Problems(1.6f));
    }

    [Fact]
    public void AFloodedWaysPassageRunsFromTheMarkerOutToTheFramesOuterFace()
    {
        Assert.Equal(new Box(8.5f, -2f, 11.5f, -1f), Hatch.Passage(ExitSide.Down, new Vector2(10f, -1f)));
        Assert.Equal(new Box(18.5f, 29.5f, 21.5f, 30.5f), Hatch.Passage(ExitSide.Up, new Vector2(20f, 29.5f)));
        Assert.Equal(new Box(57f, -16f, 59.5f, -12f), Door.Passage(ExitSide.East, new Vector2(57f, -16f)));
        Assert.Equal(new Box(-1.5f, 0f, 1f, 4f), Door.Passage(ExitSide.West, new Vector2(1f, 0f)));
    }

    [Fact]
    public void TwoRoomsFloodingAHatchFromOneDefinitionAgree()
    {
        // Each room's water fills its side of the hole out to the slab's far face: the hole is water in both, the slab solid in both.
        Threshold down = Hatch.Exit(ExitSide.Down, new Vector2(60f, -1f)), up = Hatch.Exit(ExitSide.Up, new Vector2(20f, 29.5f));
        var upper = Room(new Box(-1f, -2f, 86.5f, 24f), Hatch.Solids(ExitSide.Down, down.Marker), "Hatch", down);
        var lower = Room(new Box(-1.5f, -2f, 41f, 30.5f), Hatch.Solids(ExitSide.Up, up.Marker), "Top", up);
        upper.Waters.Add(("Lake", new Box(-3f, Hatch.Passage(ExitSide.Down, down.Marker).MinY, 90f, 10f)));
        lower.Waters.Add(("Flood", new Box(-3f, -5f, 45f, Hatch.Passage(ExitSide.Up, up.Marker).MaxY)));
        var check = new SeamCheck();
        check.AddRoom("Mere", upper);
        check.AddRoom("Underice", lower);
        check.AddWay("Mere", "Hatch", "Underice", "Top");

        Assert.Empty(check.Problems(1.6f));
    }

    // A lift shaft's head: a 3 m hole through a 1 m collar, the head lift carrying the rider the last 2.5 m.
    private static readonly SeamFrame Shaft = new(3f, 1f, 2.5f);

    [Fact]
    public void BothHeatColumnsThroughAHatchAreOneColumnAsWideAsTheFrame()
    {
        // The lower room's marker at (8, 15), the upper room's at (55, -9.5): the same place on the map, (47, -24.5) apart.
        Box below = Hatch.Heat(ExitSide.Up, new Vector2(8f, 15f), 0f);
        Box above = Hatch.Heat(ExitSide.Down, new Vector2(55f, -9.5f), 6f);

        Assert.Equal(new Box(4.5f, 0f, 11.5f, 14f), below);
        Assert.Equal(new Box(51.5f, -8.5f, 58.5f, 6f), above);
        Assert.Equal(below.MinX + 47f, above.MinX); // in line
        Assert.Equal(below.MaxY + 2f - 24.5f, above.MinY); // the slab between them, nothing else
        Assert.Throws<System.ArgumentException>(() => Hatch.Heat(ExitSide.Up, new Vector2(8f, 15f), 14.5f));
    }

    [Fact]
    public void BothLiftsThroughAHatchMeetAtOneHandOffInLineWithTheHole()
    {
        // The lower room's marker at (13, 26), the upper room's at (42, -3): the same place on the map.
        (Vector2 foot, float rise) = Shaft.Lift(ExitSide.Up, new Vector2(13f, 26f), 0f);
        (Vector2 head, float headRise) = Shaft.Lift(ExitSide.Down, new Vector2(42f, -3f), 99f);

        Assert.Equal(new Vector2(13f, 0f), foot);
        Assert.Equal(26.5f, rise);
        Assert.Equal(new Vector2(42f, -2.5f), head);
        Assert.Equal(2.5f, headRise);
        Assert.Equal(foot.Y + rise - 29f, head.Y); // the foot's upper stop is the head's lower stop, the map offset (29, -29) away
        Assert.Equal(0f, head.Y + headRise); // and the head rises to the floor
    }

    [Fact]
    public void WithoutAHeadTheLowerLiftRisesToTheFloorAndNoHeadIsPlaced()
    {
        Assert.Equal(3f, Hatch.Lift(ExitSide.Up, new Vector2(0f, 1f), -1f).Rise);
        Assert.Throws<ArgumentException>(() => Hatch.Lift(ExitSide.Down, Vector2.Zero, 0f));
        Assert.Throws<ArgumentException>(() => Shaft.Lift(ExitSide.Up, new Vector2(0f, 1f), 5f));
    }

    [Fact]
    public void TheTrimNeverEntersTheOpeningsPlayPlane()
    {
        foreach ((SeamFrame frame, ExitSide side) in new[] { (Door, ExitSide.West), (Door, ExitSide.East), (Hatch, ExitSide.Down), (Hatch, ExitSide.Up) })
        {
            IReadOnlyList<TrimPiece> trim = frame.Trim(side, lined: true, bracket: true);
            Assert.NotEmpty(trim);
            Assert.All(trim.Where(p => !p.Through), p => Assert.True(p.Proud > 0f));
            Assert.All(trim.Where(p => p.Lining && p.Through), p => Assert.True(p.Face.MaxX - p.Face.MinX < 0.2f));
        }

        Assert.DoesNotContain(Door.Trim(ExitSide.West, lined: false), p => p.Lining || p.Through);
    }

    [Fact]
    public void AFrameTakesTheLookOfTheWallItMeetsTheMost()
    {
        IReadOnlyList<Box> frame = Door.Solids(ExitSide.West, Vector2.Zero);
        var walls = new[] { new Box(2.5f, -2f, 10f, 0f), new Box(2.5f, 4f, 3f, 6f), new Box(-20f, -20f, -10f, -10f) };
        Assert.Equal(0, SeamFrame.Neighbour(frame, walls));
        Assert.Null(SeamFrame.Neighbour(frame, new[] { walls[2] }));
    }

    [Fact]
    public void ASillHangsWhenNothingCarriesItsRoomSideEnd()
    {
        Assert.False(Door.SillHangs(ExitSide.West, Vector2.Zero, new[] { new Box(2.5f, -2f, 10f, 0f) }));
        Assert.True(Door.SillHangs(ExitSide.West, Vector2.Zero, new[] { new Box(2.5f, -0.5f, 10f, 0f) }));
        Assert.False(Hatch.SillHangs(ExitSide.Down, Vector2.Zero, Array.Empty<Box>()));
    }
}
