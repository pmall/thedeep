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
}
