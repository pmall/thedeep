using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>The built world proven as a whole (WorldCheck), on a two-room world: a hall and a sanctum beside it.</summary>
public class WorldCheckTests
{
    private static readonly PlannerLimits Limits = new(
        new MoveLimits(RunSpeed: 7f, AirAcceleration: 45f, AirDeceleration: 30f, AirDrag: 12f, JumpHeight: 3.2f, JumpTimeToApex: 0.38f, JumpCutFactor: 0.45f,
            FallGravityMultiplier: 1.6f, ApexHangThreshold: 1.5f, ApexGravityMultiplier: 0.55f, MaxFallSpeed: 20f,
            DashDistance: 4.5f, DashDuration: 0.16f, DashExitSpeedFactor: 1f, AirDashes: 1),
        new HarpoonLimits(Range: 9f, MinDistance: 1.2f, ZipSpeed: 17f, ZipAcceleration: 110f, ExitSpeedFactor: 0.65f, ExitLift: 6f),
        new GripLimits(LeapAwaySpeed: 6.5f, LeapUpFactor: 0.85f, RegrabDelay: 0.25f),
        WaterMotionTests.Water,
        Rise: 0.85f * JumpMath.TakeOffSpeed(3.2f, 0.38f));

    private static readonly Vector2 Min = new(-10f, -1f), Max = new(10f, 8f);

    /// <summary>A flat room (a floor from -10 to 10 m), its exits as given, a goal box if any, waking at its middle in the start room.</summary>
    private static RoomShape Room(IEnumerable<ExitShape> exits, bool goal = false, bool start = false) => new(
        Solids: new List<Box> { new(-10f, -1f, 10f, 0f) },
        Exits: exits.ToList(),
        Wakes: start ? new List<Vector2> { new(0f, 0f) } : new List<Vector2>(),
        Anchors: new List<Vector2>(), Lifts: new List<LiftPath>(),
        Sound: new List<Box> { new(-10f, -1f, 10f, 0f) },
        Waters: new List<Box>(), Heat: new List<Box>(), Relics: new List<(Box, Ability)>(),
        Goals: goal ? new List<Box> { Box.Around(new Vector2(0f, 1f), new Vector2(2f, 2f)) } : new List<Box>(),
        Metal: new List<Box>(), Spawn: new Vector2(0f, 0f));

    private static ExitShape Doorway(string name, float x, string requires = "", float y = 1.5f) =>
        new(name, new Threshold(x > 0f ? ExitSide.East : ExitSide.West, new Vector2(x, y), new Vector2(1f, 3f)), requires, ArrivalSide: 0, Sealed: false);

    private static WorldCheck TwoRooms(string eastNeeds = "", float eastY = 1.5f)
    {
        var world = new WorldCheck(Limits);
        world.AddRoom("Hall", Min, Max, Room(new[] { Doorway("East", 9.5f, eastNeeds, eastY) }, start: true));
        world.AddRoom("Sanctum", Min, Max, Room(new[] { Doorway("West", -9.5f) }, goal: true));
        world.AddExit(new MapEdge("Hall", "East", "Sanctum", "West", Requirement.Parse(eastNeeds)), new Vector2(9.5f, eastY), new Vector2(-9.5f, 1.5f));
        world.AddExit(new MapEdge("Sanctum", "West", "Hall", "East", Requirement.None), new Vector2(-9.5f, 1.5f), new Vector2(9.5f, eastY));
        world.Graph.AddGoal("Sanctum");
        return world;
    }

    [Fact]
    public void ARoomsideWalkToTheGoalHoldsUp()
    {
        WorldCheck world = TwoRooms();
        string summary = world.Check("Hall", MapTargets.None, report: false);
        Assert.Empty(world.Problems);
        Assert.StartsWith("MAP OK: 2 rooms, 2 exits", summary);
        Assert.Contains("INSIDE OK", summary);
    }

    [Fact]
    public void TheStartMustBeARoomOfTheGame()
    {
        WorldCheck world = TwoRooms();
        world.Check("Narthex", MapTargets.None, report: false);
        Assert.Contains("map: the start room Narthex is not a room in the game", world.Problems);
    }

    [Fact]
    public void AnExitThatNeedsNothingMustBeReachedByTheBaseMoveset()
    {
        // The hall's way out hangs 12 m up: no jump reaches it, yet it claims to need nothing.
        WorldCheck world = TwoRooms(eastY: 12f);
        world.Check("Hall", MapTargets.None, report: false);
        Assert.Contains("Hall: exit East needs nothing, but the base moveset reaches it from no way into the room", world.Problems);
        Assert.Contains(world.Problems, p => p.StartsWith("map inside the rooms: no goal is reached", StringComparison.Ordinal));
    }

    [Fact]
    public void AnExitThatNeedsAnAbilityMustNotBeWalkedTo()
    {
        // Mag-grip on a way the base moveset walks through: the requirement is a lie.
        WorldCheck world = TwoRooms(eastNeeds: "MagGrip");
        world.Check("Hall", MapTargets.None, report: false);
        Assert.Contains(world.Problems, p => p.StartsWith("Hall: exit East needs MagGrip, but the base moveset reaches it", StringComparison.Ordinal));
        Assert.Contains(world.Problems, p => p.Contains("Mag-grip reaches it from no way into the room") || p.Contains("can't be reached"));
    }

    [Fact]
    public void RoomsThatOverlapOnTheMapAreAProblem()
    {
        var world = new WorldCheck(Limits);
        world.AddRoom("Hall", Min, Max, Room(new[] { Doorway("East", 0f) }, start: true));
        world.AddRoom("Sanctum", Min, Max, Room(new[] { Doorway("West", 0f) }, goal: true));
        world.AddExit(new MapEdge("Hall", "East", "Sanctum", "West", Requirement.None), new Vector2(0f, 1.5f), new Vector2(0f, 1.5f));
        world.Graph.AddGoal("Sanctum");
        world.Check("Hall", MapTargets.None, report: false);
        Assert.Contains(world.Problems, p => p.StartsWith("map layout:", StringComparison.Ordinal));
    }

    [Fact]
    public void TheReachReportSaysWhatEachWayInReaches()
    {
        WorldCheck world = TwoRooms();
        world.Check("Hall", MapTargets.None, report: false);
        List<string> lines = world.ReachReport("Hall", with: null, clumsy: false).ToList();
        Assert.Contains(lines, l => l.StartsWith("REACH Hall waking at (0, 0) [None]: exits East", StringComparison.Ordinal));
        Assert.Equal("REACH Atrium: not a room in the game", Assert.Single(world.ReachReport("Atrium", null, false)));
    }
}
