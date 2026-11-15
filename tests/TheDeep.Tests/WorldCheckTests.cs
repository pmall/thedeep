using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>The built world proven as a whole (WorldCheck), on a two-room world: a hall and a sanctum beside it.</summary>
public class WorldCheckTests
{
    private static readonly PlannerLimits Limits = TestTuning.Planner;

    private static readonly Vector2 Min = new(-10f, -1f), Max = new(10f, 8f);

    /// <summary>
    /// A flat room (a floor from -10 to 10 m) with <paramref name="metal"/> standing on it, its exits as given, a goal box if any, waking at
    /// <paramref name="wakeX"/> on the floor in the start room.
    /// </summary>
    private static RoomShape Room(IEnumerable<ExitShape> exits, bool goal = false, bool start = false, IEnumerable<Box>? metal = null, float wakeX = 0f)
    {
        List<Box> plates = metal?.ToList() ?? new List<Box>();
        return new RoomShape(
            Solids: plates.Prepend(new Box(-10f, -1f, 10f, 0f)).ToList(),
            Exits: exits.ToList(),
            Wakes: start ? new List<Vector2> { new(wakeX, 0f) } : new List<Vector2>(),
            Anchors: new List<Vector2>(), Lifts: new List<LiftPath>(),
            Sound: plates.Prepend(new Box(-10f, -1f, 10f, 0f)).ToList(),
            Waters: new List<Box>(), Heat: new List<Box>(), Relics: new List<(Box, Ability)>(),
            Goals: goal ? new List<Box> { Box.Around(new Vector2(0f, 1f), new Vector2(2f, 2f)) } : new List<Box>(),
            Metal: plates, Spawn: new Vector2(wakeX, 0f));
    }

    private static ExitShape Doorway(string name, float x, string requires = "", float y = 1.5f) =>
        new(name, new Threshold(x > 0f ? ExitSide.East : ExitSide.West, new Vector2(x, y), new Vector2(1f, 3f)), requires, ArrivalSide: 0, Sealed: false);

    /// <summary>A metal tower under the hall's way out when it hangs high: no jump reaches its top, Mag-grip climbs it.</summary>
    private static readonly Box Tower = new(7f, 0f, 9f, 10f);

    private static WorldCheck TwoRooms(string eastNeeds = "", float eastY = 1.5f, Box? hallMetal = null, float wakeX = 0f)
    {
        var world = new WorldCheck(Limits);
        world.AddRoom("Hall", Min, Max, Room(new[] { Doorway("East", 9.5f, eastNeeds, eastY) }, start: true,
            metal: hallMetal is { } m ? new[] { m } : null, wakeX: wakeX));
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
        Assert.Contains("map: Sanctum can't be reached from Hall", world.Problems);
    }

    [Fact]
    public void EachOptionOfARequirementMustReachTheExit()
    {
        // The way out on top of a metal tower: Mag-grip climbs to it, the Drill and Ballast together don't.
        WorldCheck world = TwoRooms(eastNeeds: "MagGrip | Drill & Ballast", eastY: 11.5f, hallMetal: Tower);
        world.Check("Hall", MapTargets.None, report: false);
        Assert.DoesNotContain(world.Problems, p => p.Contains("Mag-grip reaches it from no way", StringComparison.Ordinal));
        Assert.Contains("Hall: exit East needs MagGrip | Drill & Ballast, but Drill, Ballast together reaches it from no way into the room", world.Problems);
    }

    [Fact]
    public void ARoomTheWalkNeverEntersIsAProblem()
    {
        // The goal in the hall, the way to the sanctum 12 m up: the map's markers reach the sanctum, the walk never does.
        var world = new WorldCheck(Limits);
        world.AddRoom("Hall", Min, Max, Room(new[] { Doorway("East", 9.5f, y: 12f) }, goal: true, start: true));
        world.AddRoom("Sanctum", Min, Max, Room(new[] { Doorway("West", -9.5f) }));
        world.AddExit(new MapEdge("Hall", "East", "Sanctum", "West", Requirement.None), new Vector2(9.5f, 12f), new Vector2(-9.5f, 1.5f));
        world.AddExit(new MapEdge("Sanctum", "West", "Hall", "East", Requirement.None), new Vector2(-9.5f, 1.5f), new Vector2(9.5f, 12f));
        world.Graph.AddGoal("Hall");
        world.Check("Hall", MapTargets.None, report: false);
        Assert.Contains("map inside the rooms: Sanctum is never entered walking the rooms from where the pilgrim comes in", world.Problems);
        Assert.DoesNotContain(world.Problems, p => p.Contains("no goal", StringComparison.Ordinal));
    }

    [Fact]
    public void AWayOnlyNearPerfectPlayTakesIsAProblem()
    {
        // The way out on a 3 m step: the 3.2 m jump clears it, the clumsier pilgrim's doesn't.
        WorldCheck world = TwoRooms(eastY: 4.5f, hallMetal: new Box(5f, 0f, 10f, 3f));
        string summary = world.Check("Hall", MapTargets.None, report: false);
        Assert.Equal("no room for error: Hall via the start : only near-perfect play reaches exit East (a clumsier pilgrim does not; tools: --reach-report Hall --slack-report)",
            Assert.Single(world.Problems));
        Assert.Contains("INSIDE OK", summary);
    }

    [Fact]
    public void AWayIntoAnExitTheRoomLacksIsReportedAndNotTaken()
    {
        var world = new WorldCheck(Limits);
        world.AddRoom("Hall", Min, Max, Room(new[] { Doorway("East", 9.5f) }, start: true));
        world.AddRoom("Sanctum", Min, Max, Room(new[] { Doorway("West", -9.5f) }, goal: true));
        world.AddExit(new MapEdge("Hall", "East", "Sanctum", "Nowhere", Requirement.None), new Vector2(9.5f, 1.5f), null);
        world.AddExit(new MapEdge("Sanctum", "West", "Hall", "East", Requirement.None), new Vector2(-9.5f, 1.5f), new Vector2(9.5f, 1.5f));
        world.Graph.AddGoal("Sanctum");
        world.Check("Hall", MapTargets.None, report: false);
        Assert.Contains("map: Hall:East leads to Sanctum:Nowhere, which is no exit of Sanctum (the room check walks no way in there)", world.Problems);
        Assert.Contains("map inside the rooms: Sanctum is never entered walking the rooms from where the pilgrim comes in", world.Problems);
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

    [Fact]
    public void TheReachReportIsWrittenInTheInvariantCulture()
    {
        WorldCheck world = TwoRooms(hallMetal: new Box(6f, 0f, 7.5f, 1.5f), wakeX: 0.5f);
        List<string> lines = CommaDecimals.Under(() => world.ReachReport("Hall", with: null, clumsy: false).ToList());
        Assert.Contains(lines, l => l.StartsWith("REACH Hall waking at (0.5, 0)", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("REACH Hall all ledges:", StringComparison.Ordinal) && l.Contains("1.5@6..7.5", StringComparison.Ordinal));
    }
}
