using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>A heat column held to being a gate (<see cref="HeatGate"/>): a column 2 m wide across a hall 20 m long.</summary>
public class HeatGateTests
{
    private static readonly Vector2 Body = new(0.8f, 1.8f);

    private static readonly Box Floor = new(0f, -1f, 20f, 0f);

    private static List<string> Problems(Box column, List<Box> solids, Box bounds, params Box[] openings) =>
        HeatGate.Problems(column, solids, openings, bounds, Body).ToList();

    [Fact]
    public void AColumnFromFloorToCeilingIsAGate()
    {
        Assert.Empty(Problems(new Box(9f, 0f, 11f, 6f), new() { Floor, new(0f, 6f, 20f, 7f) }, new Box(0f, 0f, 20f, 6f)));
    }

    [Fact]
    public void AColumnStoppingShortOfTheCeilingFails()
    {
        List<string> problems = Problems(new Box(9f, 0f, 11f, 5f), new() { Floor, new(0f, 6f, 20f, 7f) }, new Box(0f, 0f, 20f, 6f));
        Assert.Single(problems);
        Assert.Contains("stops short", problems[0]);
    }

    [Fact]
    public void AHoodStandingAloneLeavesAWayOverIt()
    {
        List<string> problems = Problems(new Box(9f, 0f, 11f, 6f), new() { Floor, new(8f, 6f, 12f, 6.5f) }, new Box(0f, 0f, 20f, 12f));
        Assert.Single(problems);
        Assert.Contains("way round", problems[0]);
    }

    [Fact]
    public void AHoodMeetingAWallThatReachesTheRoofSealsIt()
    {
        var solids = new List<Box> { Floor, new(8f, 6f, 13f, 6.5f), new(13f, 4f, 14f, 12f), new(0f, 12f, 20f, 13f) };
        Assert.Empty(Problems(new Box(9f, 0f, 11f, 6f), solids, new Box(0f, 0f, 20f, 12f)));
    }

    [Fact]
    public void AColumnOverAPitHasNoSource()
    {
        var solids = new List<Box> { new(0f, -1f, 9.5f, 0f), new(10.5f, -1f, 20f, 0f), new(0f, 6f, 20f, 7f) };
        List<string> problems = Problems(new Box(9f, 0f, 11f, 6f), solids, new Box(0f, -1f, 20f, 6f));
        Assert.Contains(problems, p => p.Contains("foot is open"));
    }

    [Fact]
    public void AColumnMayRiseToAnExit()
    {
        var solids = new List<Box> { Floor, new(0f, 6f, 9f, 7f), new(11f, 6f, 20f, 7f) };
        Assert.Empty(Problems(new Box(9f, 0f, 11f, 6f), solids, new Box(0f, 0f, 20f, 6f), new Box(9f, 6f, 11f, 6.5f)));
    }
}
