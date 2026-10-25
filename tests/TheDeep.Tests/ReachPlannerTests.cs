using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class ReachPlannerTests
{
    // The live tuning (resources/player_tuning.tres).
    private static readonly MoveLimits Limits = new(
        RunSpeed: 7f, AirAcceleration: 45f, AirDeceleration: 30f, JumpHeight: 3.2f, JumpTimeToApex: 0.38f, JumpCutFactor: 0.45f,
        FallGravityMultiplier: 1.6f, ApexHangThreshold: 1.5f, ApexGravityMultiplier: 0.55f, MaxFallSpeed: 20f,
        DashDistance: 4.5f, DashDuration: 0.16f, DashExitSpeedFactor: 1f, AirDashes: 1);

    private static Box B(float x0, float y0, float x1, float y1) => new(x0, y0, x1, y1);

    private static bool Reaches(IEnumerable<Box> solids, Vector2 from, Box target)
    {
        var planner = new ReachPlanner(Limits, solids);
        return planner.Reach(from, Vector2.Zero, new[] { target }).Triggers.Contains(0);
    }

    [Theory]
    [InlineData(3.0f, true)]   // a full jump reaches about 3.36 m
    [InlineData(3.5f, false)]
    public void LedgeHeight(float height, bool reachable)
    {
        var solids = new[] { B(-10f, -2f, 30f, 0f), B(6f, 0f, 10f, height) };
        // Standing on top of the ledge.
        Box top = B(7.5f, height + 0.2f, 8.5f, height + 1f);
        Assert.Equal(reachable, Reaches(solids, new Vector2(0f, 0f), top));
    }

    [Theory]
    [InlineData(6f, true)]     // a running jump
    [InlineData(9f, true)]     // with the air dash
    [InlineData(13f, false)]   // beyond both
    public void GapWidth(float gap, bool reachable)
    {
        var solids = new[] { B(-10f, -2f, 0f, 0f), B(gap, -2f, gap + 10f, 0f), B(-10f, -30f, 40f, -29f) };
        Box far = B(gap + 2f, 0.1f, gap + 3f, 1f);
        Assert.Equal(reachable, Reaches(solids, new Vector2(-3f, 0f), far));
    }

    [Fact]
    public void WallsBlock()
    {
        // A wall 10 m high between the start and the target.
        var solids = new[] { B(-10f, -2f, 30f, 0f), B(4f, 0f, 5f, 10f), B(-10f, 12f, 30f, 13f) };
        Assert.False(Reaches(solids, new Vector2(0f, 0f), B(8f, 0.1f, 9f, 1f)));
    }

    [Fact]
    public void LedgesAreSplitBySolidsOnTop()
    {
        var planner = new ReachPlanner(Limits, new[] { B(0f, -1f, 20f, 0f), B(8f, 0f, 10f, 1f) });
        List<Ledge> floor = planner.Ledges.Where(l => l.Y == 0f).OrderBy(l => l.MinX).ToList();
        Assert.Equal(2, floor.Count);
        Assert.Equal(8f - 0.35f, floor[0].MaxX, 3);
        Assert.Equal(10f + 0.35f, floor[1].MinX, 3);
    }

    [Fact]
    public void FallingFromAnArrivalLandsBelow()
    {
        var solids = new[] { B(-10f, -2f, 30f, 0f) };
        var planner = new ReachPlanner(Limits, solids);
        var (_, ledges) = planner.Reach(new Vector2(5f, 20f), new Vector2(0f, -5f), new Box[0]);
        Assert.Single(ledges);
    }
}
