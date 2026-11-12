using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TheDeep.Tests;

/// <summary>
/// The ground rules (<see cref="GroundRules"/>) on a pilgrim 0.7 m wide and 1.6 m tall whose jump takes its head 4.8 m over its feet,
/// with a 0.6 m ledge assist.
/// </summary>
public class GroundRulesTests
{
    private const float Reach = 4.8f, Height = 1.6f, Radius = 0.35f, Assist = 0.6f;

    private static readonly Box Bounds = new(-100f, -100f, 100f, 100f);

    private static readonly Box[] None = System.Array.Empty<Box>();

    private static List<LowCeiling> Headroom(List<Box> solids, params int[] low) =>
        GroundRules.Headroom(solids, new HashSet<int>(low), Reach, Height, Radius, Bounds).ToList();

    [Fact]
    public void ALedgeUnderALedgeIsAPocketOverItsWholeWidth()
    {
        // A 4 m ledge under a wider ledge 4.5 m over it: no place on it to make a full jump from.
        LowCeiling low = Assert.Single(Headroom(new() { new(4f, -1f, 8f, 0f), new(2f, 4.5f, 10f, 5f) }));
        Assert.Equal((0, 1), (low.Ground, low.Ceiling));
        Assert.Equal(4.5f, low.Clearance, 3);
        Assert.Equal((4f, 8f), (low.MinX, low.MaxX));
    }

    [Fact]
    public void ACeilingOverPartOfAFloorLeavesAPlaceToJumpFromAndIsNoFinding()
    {
        Assert.Empty(Headroom(new() { new(0f, -2f, 12f, 0f), new(4f, 2f, 7f, 2.5f) }));
    }

    [Fact]
    public void AFloorSplitInFlushBlocksIsOneWalkway()
    {
        // The covered block's neighbor at the same height is a free place: no pocket.
        Assert.Empty(Headroom(new() { new(0f, -2f, 4f, 0f), new(4f, -2f, 12f, 0f), new(-1f, 4f, 4.5f, 5f) }));
    }

    [Fact]
    public void ACeilingAFullJumpUpOrTooLowToStandUnderIsNoFinding()
    {
        Assert.Empty(Headroom(new() { new(4f, -1f, 8f, 0f), new(2f, 4.8f, 10f, 5f) }));
        Assert.Empty(Headroom(new() { new(4f, -1f, 8f, 0f), new(2f, 1f, 10f, 5f) }));
    }

    [Fact]
    public void AFloorBuriedUnderAWallIsNoGround()
    {
        // The wall stands on the floor: the floor's top under it is no ground, the wall's overhang is no ceiling over it.
        Assert.Empty(Headroom(new() { new(10f, -2f, 12f, 0f), new(10f, 0f, 12f, 8f) }));
    }

    [Fact]
    public void ACeilingLowOnPurposeIsNoFindingButStillCovers()
    {
        // A corridor under a marked lintel: no finding; the same lintel joined by an unmarked slab still leaves no free place.
        Assert.Empty(Headroom(new() { new(4f, -1f, 8f, 0f), new(2f, 4f, 10f, 5f) }, 1));
        LowCeiling low = Assert.Single(Headroom(new() { new(4f, -1f, 8f, 0f), new(2f, 4f, 6f, 5f), new(6f, 4.5f, 10f, 5f) }, 1));
        Assert.Equal(2, low.Ceiling);
    }

    [Fact]
    public void TheLowestUnmarkedSolidOverAPocketIsItsCeiling()
    {
        Assert.Equal(1, Assert.Single(Headroom(new() { new(4f, -1f, 8f, 0f), new(2f, 2f, 10f, 2.5f), new(2f, 3f, 10f, 4f) })).Ceiling);
    }

    [Fact]
    public void ASmallStepIsASeamAndFlushOrTallStepsAreNot()
    {
        Seam step = Assert.Single(GroundRules.Seams(new List<Box> { new(0f, -2f, 4f, 0f), new(4f, -2f, 7f, 0.5f) }, Assist, None));
        Assert.Equal(SeamKind.Step, step.Kind);
        Assert.Equal(0.5f, step.Size, 3);
        Assert.Equal(4f, step.X, 3);

        Assert.Empty(GroundRules.Seams(new List<Box> { new(0f, -2f, 4f, 0f), new(4f, -2f, 7f, 0f) }, Assist, None));
        Assert.Empty(GroundRules.Seams(new List<Box> { new(0f, -2f, 4f, 0f), new(4f, -2f, 7f, 2.5f) }, Assist, None));
    }

    [Fact]
    public void AStepUnderASolidInWaterOrUnderAMachineAtRestIsNotOnARunningLine()
    {
        var step = new List<Box> { new(0f, -2f, 4f, 0f), new(4f, -2f, 7f, 0.5f) };
        Assert.Empty(GroundRules.Seams(step.Append(new Box(3f, 0f, 5f, 3f)).ToList(), Assist, None));
        Assert.Empty(GroundRules.Seams(step, Assist, new[] { new Box(-10f, -5f, 10f, 5f) }));
        Assert.Empty(GroundRules.Seams(step, Assist, new[] { new Box(1f, -0.5f, 4f, 0.5f) }));
    }

    [Fact]
    public void ASolidReachingIntoAFramesColumnIntrudesAndAFramesOwnPiecesDoNot()
    {
        var solids = new List<Box> { new(0f, 4f, 2f, 10f), new(0f, -2f, 2f, 0f), new(1.5f, 6f, 6f, 10f), new(2f, 0f, 6f, 6f) };
        (int a, int b, Box o) = Assert.Single(GroundRules.Intrusions(solids, new int?[] { 0, 0, null, null }));
        Assert.Equal((0, 2), (a, b));
        Assert.Equal(new Box(1.5f, 6f, 2f, 10f), o);
        Assert.Single(GroundRules.Intrusions(solids, new int?[] { 0, 1, 1, null })); // two frames overlapping: the second's piece in the first's
    }

    [Fact]
    public void OverlappingSolidsWithATopInOnePlaneShareAFace()
    {
        Seam face = Assert.Single(GroundRules.Seams(new List<Box> { new(0f, -2f, 6f, 0f), new(4f, -1f, 9f, 0f) }, Assist, None));
        Assert.Equal(SeamKind.Coplanar, face.Kind);
        Assert.Equal(0f, face.Y, 3);
        Assert.Equal(2f, face.Size, 3);
    }

    [Fact]
    public void AMeshAcrossAShaftIsOpenBetweenItsWalls()
    {
        var walls = new List<Box> { new(58f, -30f, 60f, -6f), new(63f, -30f, 65f, -6f) };
        (float from, float to) = Assert.Single(GroundRules.OpenSpans(new Box(58f, -10f, 65f, -9f), walls));
        Assert.Equal((60f, 63f), (from, to));
        Assert.Empty(GroundRules.OpenSpans(new Box(58f, -10f, 60f, -9f), walls));
    }
}
