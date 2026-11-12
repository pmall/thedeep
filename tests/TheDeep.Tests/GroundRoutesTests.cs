using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>The ground routes the route check plays (GroundRoutes): which of the planner's flights are picked, and how a real one is judged.</summary>
public class GroundRoutesTests
{
    private static readonly PlannerLimits Limits = new(
        new MoveLimits(RunSpeed: 7f, AirAcceleration: 45f, AirDeceleration: 30f, AirDrag: 12f, AirCarrySpeed: 11.5f, JumpHeight: 3.2f, JumpTimeToApex: 0.38f, JumpCutFactor: 0.45f,
            FallGravityMultiplier: 1.6f, ApexHangThreshold: 1.5f, ApexGravityMultiplier: 0.55f, MaxFallSpeed: 20f,
            DashDistance: 4.5f, DashDuration: 0.16f, DashExitSpeedFactor: 1f, AirDashes: 1),
        new HarpoonLimits(Range: 9f, MinDistance: 1.2f, ZipSpeed: 17f, ZipAcceleration: 110f, ExitSpeedFactor: 0.65f, ExitLift: 6f),
        new GripLimits(LeapAwaySpeed: 6.5f, LeapUpFactor: 0.85f, RegrabDelay: 0.25f),
        WaterMotionTests.Water,
        Rise: 0.85f * JumpMath.TakeOffSpeed(3.2f, 0.38f));

    /// <summary>
    /// A hall walled at ±21 m, in three steps down eastward: a high floor at 3 m west of x −4, then a floor at 0 to x 8, then a pit at −3,
    /// the pilgrim waking up on the high floor.
    /// </summary>
    private static RoomShape Step(List<ExitShape>? exits = null)
    {
        var solids = new List<Box>
        {
            new(-21f, -4f, 21f, -3f), new(-21f, -3f, 8f, 0f), new(-21f, 0f, -4f, 3f), new(-22f, -4f, -21f, 20f), new(21f, -4f, 22f, 20f),
        };
        return new RoomShape(solids, exits ?? new List<ExitShape>(), new List<Vector2> { new(-10f, 3f) }, new List<Vector2>(), new List<LiftPath>(),
            solids, new List<Box>(), new List<Box>(), new List<(Box, Ability)>(), new List<Box>(), new List<Box>(), new Vector2(-10f, 3f));
    }

    [Fact]
    public void EachKindOfMoveIsPlayedBetweenFloorsNoOtherRouteJoins()
    {
        List<GroundRoute> routes = GroundRoutes.Pick(Step(), Limits, 8);
        Assert.True(routes.Count >= 5, string.Join("\n", routes.Select(r => r.What)));
        Assert.Equal(routes.Count, routes.Select(r => (r.Leap.From, r.Leap.Lands)).Distinct().Count());
        Assert.Contains(routes, r => r.Leap.Move is { JumpTicks: -1, DashTick: >= 0 });
        Assert.Contains(routes, r => r.Leap.Move is { JumpTicks: 0, DashTick: >= 0 }); // walking off the step, then dashing
        Assert.All(routes, r => Assert.True(r.Leap.Clean));
    }

    [Fact]
    public void ARunningTakeOffStartsARunUpBehindIt()
    {
        foreach (GroundRoute r in GroundRoutes.Pick(Step(), Limits, 8).Where(r => r.Leap.Move.Dir != 0))
        {
            Assert.Equal(r.Leap.TakeOff.Y, r.Start.Y);
            Assert.Equal(-r.Leap.Move.Dir * 7f * GroundRoutes.RunUpTicks / 60f, r.Start.X - r.Leap.TakeOff.X, 3);
        }
    }

    [Fact]
    public void RoutesKeepOffExits()
    {
        // A doorway at the pit's east end: no route comes near it.
        var east = new ExitShape("East", new Threshold(ExitSide.East, new Vector2(20.5f, -3f), new Vector2(1f, 3f)), "", 0, false);
        foreach (GroundRoute r in GroundRoutes.Pick(Step(new List<ExitShape> { east }), Limits, 8))
        {
            Assert.All(r.Leap.Path, p => Assert.False(Box.Around(p + new Vector2(0f, 0.8f), new Vector2(0.7f, 1.6f)).Grown(GroundRoutes.KeepOff)
                .Overlaps(east.Threshold.Trigger)));
        }
    }

    [Fact]
    public void ARealFlightIsJudgedByItsLandingAndItsPath()
    {
        GroundRoute route = GroundRoutes.Pick(Step(), Limits, 8).First(r => r.Leap.Move.JumpTicks < 0);
        var path = route.Leap.Path.ToList();
        Assert.Null(GroundRoutes.Mismatch(route, path, landed: true));
        Assert.Contains("never landed", GroundRoutes.Mismatch(route, path, landed: false));

        // A jump that peaks higher than planned, landing in the same place.
        var higher = path.Select((p, i) => i == 0 || i == path.Count - 1 ? p : p + new Vector2(0f, 0.4f)).ToList();
        Assert.Contains("strayed", GroundRoutes.Mismatch(route, higher, landed: true));

        // Landing short.
        var shorter = path.Take(path.Count - 1).Append(path[^1] - new Vector2(0.6f * route.Leap.Move.Dir + 0.6f, 0f)).ToList();
        Assert.Contains("landed at", GroundRoutes.Mismatch(route, shorter, landed: true));
    }
}
