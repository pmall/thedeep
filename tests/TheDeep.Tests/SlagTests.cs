using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TheDeep.Tests;

/// <summary>The burn rule (<see cref="Burn"/>) and a slag body's basin (<see cref="SlagBasin"/>), on a channel 4 m wide, 2 m deep.</summary>
public class SlagTests
{
    /// <summary>The slag: x 18..22, from its bed at -2 to its surface at 0.</summary>
    private static readonly Box Slag = new(18f, -2f, 22f, 0f);

    /// <summary>Its basin: two banks up to 1, a bed under it.</summary>
    private static List<Box> Basin() => new() { new(9f, -3f, 18f, 1f), new(22f, -3f, 42f, 1f), new(18f, -3f, 22f, -2f) };

    [Fact]
    public void ABodySharingAreaWithItIsBurned()
    {
        Assert.Equal(BurnReach.Touching, Burn.Reach(Slag, Slag.Grown(0.75f), new Box(17.8f, -0.4f, 18.4f, 1.4f)));
    }

    [Fact]
    public void ABodyBesideItWithinTheMarginIsNearAndBeyondItClear()
    {
        Assert.Equal(BurnReach.Near, Burn.Reach(Slag, Slag.Grown(0.75f), new Box(17f, 0.2f, 17.6f, 2f)));
        Assert.Equal(BurnReach.Clear, Burn.Reach(Slag, Slag.Grown(0.75f), new Box(16f, 0.2f, 16.6f, 2f)));
    }

    [Fact]
    public void ABodyTouchingOnlyItsEdgeIsNotBurned()
    {
        Assert.Equal(BurnReach.Near, Burn.Reach(Slag, Slag.Grown(0.75f), new Box(17.4f, 0f, 18f, 1.8f)));
    }

    [Fact]
    public void ABodyOnALipOverItIsNearWhenTheKeepOffReachesUp()
    {
        var lip = new Box(17.9f, 1f, 18.5f, 2.8f);
        Assert.Equal(BurnReach.Clear, Burn.Reach(Slag, Slag.Grown(0.75f), lip));
        Assert.Equal(BurnReach.Near, Burn.Reach(Slag, Slag with { MinX = 17.25f, MaxY = 2.5f }, lip));
    }

    [Fact]
    public void AFullBasinHolds()
    {
        Assert.Empty(SlagBasin.Problems(Slag, Basin()));
    }

    [Fact]
    public void AFullBasinHasNoSunkenGround()
    {
        List<Box> solids = Basin();
        solids.Add(new Box(19.5f, -3f, 20.5f, 0f)); // a pillar flush with the surface: rock to stand on
        Assert.Empty(SlagBasin.SunkenGround(Slag, solids, 0.3f));
    }

    [Fact]
    public void APillarSunkUnderTheSurfaceIsGroundInTheSlag()
    {
        List<Box> solids = Basin();
        solids.Add(new Box(19.5f, -3f, 20.5f, -1f));
        Assert.Single(SlagBasin.SunkenGround(Slag, solids, 0.3f));
    }

    [Fact]
    public void ABankSteppingDownBesideItWithinAHalfWidthIsGroundInTheSlag()
    {
        List<Box> solids = Basin();
        solids[0] = new Box(9f, -3f, 17.9f, 1f);
        solids.Add(new Box(17.9f, -3f, 18f, -0.5f)); // the bank's last 0.1 m, half a meter under the surface
        Assert.Single(SlagBasin.SunkenGround(Slag, solids, 0.3f));
        Assert.Empty(SlagBasin.SunkenGround(Slag, solids.Append(new Box(17.9f, -0.5f, 18f, 1f)).ToList(), 0.3f));
    }

    [Fact]
    public void APillarRisingFromTheBedIsPartOfIt()
    {
        List<Box> solids = Basin();
        solids[2] = new Box(18f, -3f, 19.5f, -2f);
        solids.Add(new Box(19.5f, -3f, 20.5f, 1f));
        solids.Add(new Box(20.5f, -3f, 22f, -2f));
        Assert.Empty(SlagBasin.Problems(Slag, solids));
    }

    [Fact]
    public void ABedOpenUnderPartOfItFails()
    {
        List<Box> solids = Basin();
        solids[2] = new Box(18f, -3f, 20f, -2f);
        string problem = Assert.Single(SlagBasin.Problems(Slag, solids));
        Assert.Contains("bed", problem);
        Assert.Contains("from x 20 to 22", problem);
    }

    [Fact]
    public void SlagFloatingOverNothingFailsItsBed()
    {
        Assert.Contains(SlagBasin.Problems(Slag, Basin().Take(2).ToList()), p => p.Contains("bed"));
    }

    [Fact]
    public void ABankLowerThanTheSurfaceFails()
    {
        List<Box> solids = Basin();
        solids[1] = new Box(22f, -3f, 42f, -1f);
        string problem = Assert.Single(SlagBasin.Problems(Slag, solids));
        Assert.Contains("east bank", problem);
        Assert.Contains("from y -1 to 0", problem);
    }
}
