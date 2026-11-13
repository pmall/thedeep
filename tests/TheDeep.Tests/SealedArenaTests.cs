using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>
/// A sealed arena held to its room (<see cref="SealedArena"/>): a hall 20 m long under a roof 5 m up, a doorway at each end, the arena
/// between two gates 7 m either side of the middle.
/// </summary>
public class SealedArenaTests
{
    private static readonly PlannerLimits Limits = new(
        new MoveLimits(RunSpeed: 7f, AirAcceleration: 45f, AirDeceleration: 30f, AirDrag: 12f, AirCarrySpeed: 11.5f, JumpHeight: 3.2f, JumpTimeToApex: 0.38f, JumpCutFactor: 0.45f,
            FallGravityMultiplier: 1.6f, ApexHangThreshold: 1.5f, ApexGravityMultiplier: 0.55f, MaxFallSpeed: 20f,
            DashDistance: 4.5f, DashDuration: 0.16f, DashExitSpeedFactor: 1f, AirDashes: 1),
        new HarpoonLimits(Range: 9f, MinDistance: 1.2f, ZipSpeed: 17f, ZipAcceleration: 110f, ExitSpeedFactor: 0.65f, ExitLift: 6f),
        new GripLimits(LeapAwaySpeed: 6.5f, LeapUpFactor: 0.85f, RegrabDelay: 0.25f),
        WaterMotionTests.Water,
        Rise: 0.85f * JumpMath.TakeOffSpeed(3.2f, 0.38f));

    private static readonly Box Bounds = new(-10f, -1f, 10f, 6f);

    private static readonly List<(string, Box)> WaysOut = new() { ("West", new Box(-10f, 0f, -9f, 3f)), ("East", new Box(9f, 0f, 10f, 3f)) };

    private static RoomShape Hall()
    {
        var solids = new List<Box> { new(-10f, -1f, 10f, 0f), new(-10f, 5f, 10f, 6f) };
        return new RoomShape(solids, new List<ExitShape>(), new List<Vector2>(), new List<Vector2>(), new List<LiftPath>(), solids.ToList(),
            new List<Box>(), new List<Box>(), new List<(Box, Ability)>(), new List<Box>(), new List<Box>(), Vector2.Zero);
    }

    /// <summary>A gate of the given height standing on the floor at <paramref name="x"/>.</summary>
    private static Box Gate(float x, float height = 5f) => new(x - 0.3f, 0f, x + 0.3f, height);

    private static SealedArena Arena(Box? area = null, Vector2? foe = null, params Box[] gates) =>
        new("Fight", area ?? new Box(-6f, -1f, 6f, 4f), gates.Length > 0 ? gates : new[] { Gate(-7f), Gate(7f) },
            new[] { ("Warden", foe ?? Vector2.Zero) });

    private static List<string> Problems(SealedArena arena) => arena.Problems(Hall(), Bounds, WaysOut, Limits, WorldCheck.Simulated).ToList();

    [Fact]
    public void ArenaSealedByItsGatesHoldsUp()
    {
        Assert.Empty(Problems(Arena()));
    }

    [Fact]
    public void AWayOutNoGateBarsFails()
    {
        List<string> problems = Problems(Arena(gates: Gate(7f)));
        Assert.Equal(new[] { "arena Fight: its gates shut, the pilgrim still leaves its area by exit West (MagGrip, Harpoon, Drill, Ballast, HeatPlating): a gate must bar every way out" },
            problems);
    }

    [Fact]
    public void AGateTheBodyJumpsOverFails()
    {
        List<string> problems = Problems(Arena(gates: new[] { Gate(-7f), Gate(7f, height: 1f) }));
        Assert.Single(problems);
        Assert.Contains("by exit East", problems[0]);
    }

    [Fact]
    public void AFoeOutsideTheAreaFails()
    {
        List<string> problems = Problems(Arena(foe: new Vector2(8f, 0f)));
        Assert.Equal(new[] { "arena Fight: its foe Warden stands at (8, 0), outside its area (-6, -1)..(6, 4)" }, problems);
    }

    [Fact]
    public void AnAreaPastTheCameraBoundsFails()
    {
        List<string> problems = Problems(Arena(area: new Box(-6f, -2f, 6f, 4f)));
        Assert.Equal(new[] { "arena Fight: its area (-6, -2)..(6, 4) reaches past the room's camera bounds (-10, -1)..(10, 6)" }, problems);
    }

    [Fact]
    public void AnAreaWithNoFloorFails()
    {
        List<string> problems = Problems(Arena(area: new Box(-6f, 1f, 6f, 4f), foe: new Vector2(0f, 2f)));
        Assert.Equal(new[] { "arena Fight: no floor lies inside its area (-6, 1)..(6, 4), so the fight never starts" }, problems);
    }
}
