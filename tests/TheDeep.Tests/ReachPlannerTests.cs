using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class ReachPlannerTests
{
    // The live tuning (resources/player_tuning.tres).
    private static readonly MoveLimits Limits = new(
        RunSpeed: 7f, AirAcceleration: 45f, AirDeceleration: 30f, AirDrag: 12f, JumpHeight: 3.2f, JumpTimeToApex: 0.38f, JumpCutFactor: 0.45f,
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
    [InlineData(3.6f, true)]   // just out of a jump's reach: the ledge assist lifts the pilgrim onto it
    [InlineData(4.2f, false)]  // beyond the assist
    public void LedgeAssistLiftsOntoALedgeJustOutOfReach(float height, bool reachable)
    {
        var solids = new[] { B(-10f, -2f, 30f, 0f), B(6f, 0f, 10f, height) };
        Box top = B(7.5f, height + 0.2f, 8.5f, height + 1f);
        Assert.False(Reaches(solids, new Vector2(0f, 0f), top));
        var assisted = new ReachPlanner(Limits with { LedgeAssist = 0.6f }, solids);
        Assert.Equal(reachable, assisted.Reach(new Vector2(0f, 0f), Vector2.Zero, new[] { top }).Triggers.Contains(0));
        Assert.Equal(0f, (Limits with { LedgeAssist = 0.6f }).Clumsy().LedgeAssist);
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
        var (_, ledges) = planner.Reach(new Vector2(5f, 20f), new Vector2(0f, -5f), Array.Empty<Box>());
        Assert.Single(ledges);
    }

    private static readonly HarpoonLimits Harpoon = new(Range: 9f, MinDistance: 1.2f, ZipSpeed: 17f, ZipAcceleration: 110f,
        ExitSpeedFactor: 0.5f, ExitLift: 6f);

    [Fact]
    public void AnAnchorLiftsThePilgrimOntoAHighLedge()
    {
        // A ledge 8 m up, out of any jump's reach; an anchor hangs over its lip.
        var solids = new[] { B(-10f, -2f, 30f, 0f), B(10f, 0f, 20f, 8f) };
        Box top = B(14f, 8.2f, 15f, 9f);
        var anchors = new[] { new Vector2(9f, 9.5f) };
        Assert.DoesNotContain(0, new ReachPlanner(Limits, solids).Reach(Vector2.Zero, Vector2.Zero, new[] { top }).Triggers);
        Assert.Contains(0, new ReachPlanner(Limits, solids, Harpoon, anchors).Reach(Vector2.Zero, Vector2.Zero, new[] { top }).Triggers);
    }

    [Fact]
    public void AnchorsChainAcrossAGapAndNeedSight()
    {
        // A 26 m chasm: no jump crosses it; two anchors 8 m apart do. A wall between them breaks the chain.
        var solids = new List<Box> { B(-10f, -2f, 0f, 0f), B(26f, -2f, 36f, 0f), B(-10f, -40f, 40f, -39f) };
        Box far = B(30f, 0.1f, 31f, 1f);
        var anchors = new[] { new Vector2(6f, 5f), new Vector2(13f, 5.5f), new Vector2(20f, 5f) };
        Assert.Contains(0, new ReachPlanner(Limits, solids, Harpoon, anchors).Reach(new Vector2(-1f, 0f), Vector2.Zero, new[] { far }).Triggers);
        solids.Add(B(16f, 2f, 17f, 12f));
        Assert.DoesNotContain(0, new ReachPlanner(Limits, solids, Harpoon, anchors).Reach(new Vector2(-1f, 0f), Vector2.Zero, new[] { far }).Triggers);
    }

    [Fact]
    public void AnchorsOutOfRangeAreNotReached()
    {
        var solids = new[] { B(-10f, -2f, 30f, 0f), B(10f, 0f, 20f, 8f) };
        Box top = B(14f, 8.2f, 15f, 9f);
        var anchors = new[] { new Vector2(9f, 16f) };
        Assert.DoesNotContain(0, new ReachPlanner(Limits, solids, Harpoon, anchors).Reach(Vector2.Zero, Vector2.Zero, new[] { top }).Triggers);
    }

    [Fact]
    public void ALiftCarriesThePilgrimUpItsShaft()
    {
        // A hatch 20 m up, over a lift's platform set flush in the floor beside the start.
        var solids = new[] { B(-10f, -2f, 4f, 0f), B(7f, -2f, 20f, 0f), B(4f, -3f, 7f, -0.5f) };
        Box hatch = B(4f, 21f, 7f, 22f);
        var lift = new LiftPath(B(4f, -0.5f, 7f, 0f), 20.5f);
        Assert.DoesNotContain(0, new ReachPlanner(Limits, solids).Reach(Vector2.Zero, Vector2.Zero, new[] { hatch }).Triggers);
        Assert.Contains(0, new ReachPlanner(Limits, solids, lifts: new[] { lift }).Reach(Vector2.Zero, Vector2.Zero, new[] { hatch }).Triggers);

        // One that rises short of it doesn't.
        var shorter = lift with { Rise = 15f };
        Assert.DoesNotContain(0, new ReachPlanner(Limits, solids, lifts: new[] { shorter }).Reach(Vector2.Zero, Vector2.Zero, new[] { hatch }).Triggers);
    }

    // A pool 8 m deep between two banks, its surface level with the near bank; a doorway low in its far wall.
    private static readonly Box[] Pool = { B(-10f, -10f, 0f, 0f), B(0f, -10f, 20f, -8f), B(20f, -6f, 30f, 1.5f), B(20f, -10f, 30f, -8f), B(30f, -10f, 31f, 10f) };
    private static readonly Box PoolWater = B(0f, -8f, 20f, 0f);
    private static readonly Box Doorway = B(20f, -8f, 21f, -6f);

    private static ReachPlanner Swimmer(bool ballast = false) =>
        new(Limits, Pool, water: WaterMotionTests.Water, waters: new[] { PoolWater }, ballast: ballast);

    [Fact]
    public void FloatingCrossesAPoolAndJumpsOntoTheFarBank()
    {
        Box farBank = B(24f, 1.6f, 25f, 2.5f);
        Assert.Contains(0, Swimmer().Reach(new Vector2(-5f, 0f), Vector2.Zero, new[] { farBank }).Triggers);
        Assert.Contains(Swimmer().Ledges, l => l.Swim && l.MinX <= 1f && l.MaxX >= 19f);
    }

    [Fact]
    public void WithoutBallastTheBottomIsOutOfReach()
    {
        // Not walking in, nor falling from 12 m up: the water holds the pilgrim up.
        Assert.DoesNotContain(0, Swimmer().Reach(new Vector2(-5f, 0f), Vector2.Zero, new[] { Doorway }).Triggers);
        Assert.DoesNotContain(0, Swimmer().Reach(new Vector2(10f, 12f), Vector2.Zero, new[] { Doorway }).Triggers);
    }

    [Fact]
    public void BallastSinksToTheBottom()
    {
        Assert.Contains(0, Swimmer(ballast: true).Reach(new Vector2(-5f, 0f), Vector2.Zero, new[] { Doorway }).Triggers);
    }

    [Fact]
    public void HeatIsNoGoWithoutHeatPlating()
    {
        // A column of heat across a corridor: the base moveset never passes it (not even jumping: it reaches the roof).
        var solids = new[] { B(-10f, -2f, 30f, 0f), B(-10f, 6f, 30f, 8f) };
        Box far = B(20f, 0.1f, 21f, 1f);
        var heat = new[] { B(9f, 0f, 11f, 6f) };
        Assert.DoesNotContain(0, new ReachPlanner(Limits, solids, hazards: heat).Reach(Vector2.Zero, Vector2.Zero, new[] { far }).Triggers);
        Assert.Contains(0, new ReachPlanner(Limits, solids).Reach(Vector2.Zero, Vector2.Zero, new[] { far }).Triggers);

        // A low vent is jumped over.
        var low = new[] { B(9f, 0f, 11f, 1.5f) };
        Assert.Contains(0, new ReachPlanner(Limits, solids, hazards: low).Reach(Vector2.Zero, Vector2.Zero, new[] { far }).Triggers);
    }

    // Mag-grip's live tuning.
    private static readonly GripLimits Grip = new(LeapAwaySpeed: 6.5f, LeapUpFactor: 0.85f, RegrabDelay: 0.25f);

    private static bool Grips(IEnumerable<Box> solids, IEnumerable<Box> metal, Vector2 from, Box target) =>
        new ReachPlanner(Limits, solids, grip: Grip, metal: metal).Reach(from, Vector2.Zero, new[] { target }).Triggers.Contains(0);

    [Fact]
    public void GripClimbsAMetalWallOntoItsTop()
    {
        // A 10 m metal tower: no jump reaches its top; gripping its side walks up and round onto it.
        Box tower = B(4f, 0f, 6f, 10f);
        var solids = new[] { B(-10f, -2f, 30f, 0f), tower, B(-10f, 20f, 30f, 21f) };
        Box top = B(4.5f, 10.2f, 5.5f, 11f);
        Assert.False(Reaches(solids, new Vector2(0f, 0f), top));
        Assert.True(Grips(solids, new[] { tower }, new Vector2(0f, 0f), top));
    }

    [Fact]
    public void GripNeedsMetal()
    {
        Box tower = B(4f, 0f, 6f, 10f);
        var solids = new[] { B(-10f, -2f, 30f, 0f), tower, B(-10f, 20f, 30f, 21f) };
        Assert.False(Grips(solids, Array.Empty<Box>(), new Vector2(0f, 0f), B(4.5f, 10.2f, 5.5f, 11f)));
    }

    [Fact]
    public void GripWalksUnderACeilingAndDropsBeyondAWall()
    {
        // A rock wall too high to jump: up the metal pillar, a jump from its top onto the metal slab's underside, along under it past
        // the wall, and a drop on the far side.
        Box pillar = B(2f, 0f, 3f, 6f);
        Box slab = B(2f, 8f, 14f, 9f);
        Box wall = B(7f, 0f, 8f, 4.5f);
        var solids = new[] { B(-10f, -2f, 30f, 0f), pillar, slab, wall, B(-10f, 20f, 30f, 21f) };
        Box beyond = B(10f, 0.1f, 11f, 1f);
        Assert.False(Reaches(solids, new Vector2(0f, 0f), beyond));
        Assert.True(Grips(solids, new[] { pillar, slab }, new Vector2(0f, 0f), beyond));
    }

    [Fact]
    public void GripLetsGoWhereTheMetalEnds()
    {
        // A tower with a metal foot and rock above it in the same plane: the grip holds only on the metal.
        Box foot = B(4f, 0f, 6f, 2f);
        var solids = new[] { B(-10f, -2f, 30f, 0f), foot, B(4f, 2f, 6f, 10f), B(-10f, 20f, 30f, 21f) };
        Assert.False(Grips(solids, new[] { foot }, new Vector2(0f, 0f), B(4.5f, 10.2f, 5.5f, 11f)));
    }

    [Fact]
    public void WaterLetsGoOfAGrip()
    {
        // The far bank's face is metal down into the pool: a grip holds only above the water, so the doorway low in the far wall stays
        // Ballast's.
        var planner = new ReachPlanner(Limits, Pool, water: WaterMotionTests.Water, waters: new[] { PoolWater }, grip: Grip, metal: new[] { Pool[2] });
        Box onTheFace = B(19.5f, 0.2f, 20f, 0.6f);
        (HashSet<int> reached, _) = planner.Reach(new Vector2(-5f, 0f), Vector2.Zero, new[] { Doorway, onTheFace });
        Assert.Contains(1, reached);
        Assert.DoesNotContain(0, reached);
    }

    [Fact]
    public void GripCatchesAMetalWallAcrossAPit()
    {
        // A jump over a pit into the face of a metal tower: gripped in the air, climbed onto its top.
        Box tower = B(6f, -30f, 9f, 12f);
        var solids = new[] { B(-20f, -2f, 0f, 0f), tower, B(-20f, -32f, 40f, -30f) };
        Box top = B(7f, 12.1f, 8f, 13f);
        Assert.False(Reaches(solids, new Vector2(-3f, 0f), top));
        Assert.True(Grips(solids, new[] { tower }, new Vector2(-3f, 0f), top));
    }
}
