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

    private static List<LowCeiling> Headroom(List<Box> solids, params Box[] low) => GroundRules.Headroom(solids, Reach, Height, Radius, low, Bounds).ToList();

    [Fact]
    public void ALedgeTwoMetersOverAFloorIsLowHeadroomOverItsWidthAndTheCapsulesRadius()
    {
        List<LowCeiling> found = Headroom(new() { new(0f, -2f, 12f, 0f), new(4f, 2f, 7f, 2.5f) });
        LowCeiling low = Assert.Single(found);
        Assert.Equal((0, 1), (low.Ground, low.Ceiling));
        Assert.Equal(2f, low.Clearance, 3);
        Assert.Equal(3.65f, low.MinX, 3);
        Assert.Equal(7.35f, low.MaxX, 3);
    }

    [Fact]
    public void ACeilingAFullJumpUpOrTooLowToStandUnderIsNoFinding()
    {
        Assert.Empty(Headroom(new() { new(0f, -2f, 12f, 0f), new(4f, 4.8f, 7f, 5f) }));
        Assert.Empty(Headroom(new() { new(0f, -2f, 12f, 0f), new(4f, 1f, 7f, 5f) }));
    }

    [Fact]
    public void AFloorBuriedUnderAWallIsNoGround()
    {
        // The wall stands on the floor: the floor's top under it is no ground, the wall's overhang is no ceiling over it.
        Assert.Empty(Headroom(new() { new(0f, -2f, 12f, 0f), new(10f, 0f, 12f, 8f) }));
    }

    [Fact]
    public void APassageMarkedLowIsExcused()
    {
        Assert.Empty(Headroom(new() { new(0f, -2f, 12f, 0f), new(4f, 2f, 7f, 2.5f) }, new Box(3f, 0f, 8f, 4f)));
    }

    [Fact]
    public void OnlyTheLowestSolidOverAStretchCounts()
    {
        List<LowCeiling> found = Headroom(new() { new(0f, -2f, 12f, 0f), new(4f, 2f, 7f, 2.5f), new(4f, 3f, 7f, 4f) });
        Assert.Equal(1, Assert.Single(found).Ceiling);
    }

    [Fact]
    public void TopsMeetingFlushOrByASmallStepAreSeamsAndATallStepIsNot()
    {
        Seam flush = Assert.Single(GroundRules.Seams(new List<Box> { new(0f, -2f, 4f, 0f), new(4f, -2f, 7f, 0f) }, Assist));
        Assert.Equal(SeamKind.Flush, flush.Kind);
        Assert.Equal(4f, flush.X, 3);

        Seam step = Assert.Single(GroundRules.Seams(new List<Box> { new(0f, -2f, 4f, 0f), new(4f, -2f, 7f, 0.5f) }, Assist));
        Assert.Equal(SeamKind.Step, step.Kind);
        Assert.Equal(0.5f, step.Size, 3);

        Assert.Empty(GroundRules.Seams(new List<Box> { new(0f, -2f, 4f, 0f), new(4f, -2f, 7f, 2.5f) }, Assist));
    }

    [Fact]
    public void ASeamUnderAThirdSolidIsNotOnAWalkingSurface()
    {
        var solids = new List<Box> { new(0f, -2f, 4f, 0f), new(4f, -2f, 7f, 0f), new(3f, 0f, 5f, 3f) };
        Assert.DoesNotContain(GroundRules.Seams(solids, Assist), s => s.Kind == SeamKind.Flush);
    }

    [Fact]
    public void OverlappingSolidsWithATopInOnePlaneShareAFace()
    {
        Seam face = Assert.Single(GroundRules.Seams(new List<Box> { new(0f, -2f, 6f, 0f), new(4f, -1f, 9f, 0f) }, Assist));
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
