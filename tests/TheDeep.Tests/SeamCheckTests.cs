using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

/// <summary>The rooms held to each other across their exits (SeamCheck), on a loft over a well: a hole in the loft's floor onto the well's roof.</summary>
public class SeamCheckTests
{
    private const float Reach = 1.8f;

    /// <summary>
    /// The loft: a floor slab from -10 to 10 at y -1..0, cut by a hole <paramref name="hole"/> wide at x 0 (its exit Down at (0, -0.5)),
    /// shown down to y -3.
    /// </summary>
    private static SeamRoom Loft(float hole = 2f, List<(string, Box)>? waters = null, List<LiftPath>? lifts = null) => new(
        new Box(-10f, -3f, 10f, 8f),
        new List<Box> { new(-10f, -1f, -hole / 2f, 0f), new(hole / 2f, -1f, 10f, 0f) },
        waters ?? new List<(string, Box)>(), lifts ?? new List<LiftPath>(),
        new Dictionary<string, Threshold> { ["Hole"] = new(ExitSide.Down, new Vector2(0f, -0.5f), new Vector2(hole, 1f)) });

    /// <summary>
    /// The well, below: its roof slab at y 9..10 cut by a hole <paramref name="hole"/> wide at x 0 (its exit Up at (0, 9.5)), its floor at
    /// y -1..0, shown up to y 12.
    /// </summary>
    private static SeamRoom Well(float hole = 2f, List<(string, Box)>? waters = null, List<LiftPath>? lifts = null) => new(
        new Box(-10f, -1f, 10f, 12f),
        new List<Box> { new(-10f, 9f, -hole / 2f, 10f), new(hole / 2f, 9f, 10f, 10f), new(-10f, -1f, 10f, 0f) },
        waters ?? new List<(string, Box)>(), lifts ?? new List<LiftPath>(),
        new Dictionary<string, Threshold> { ["Roof"] = new(ExitSide.Up, new Vector2(0f, 9.5f), new Vector2(hole, 1f)) });

    private static List<SeamProblem> Check(SeamRoom loft, SeamRoom well)
    {
        var seams = new SeamCheck();
        seams.AddRoom("Loft", loft);
        seams.AddRoom("Well", well);
        seams.AddWay("Well", "Roof", "Loft", "Hole");
        seams.AddWay("Loft", "Hole", "Well", "Roof");
        return seams.Problems(Reach).ToList();
    }

    [Fact]
    public void MatchingHolesAgree()
    {
        Assert.Empty(Check(Loft(), Well()));
    }

    [Fact]
    public void AHoleWiderThanTheOneItLandsUnderFailsNamingBothEnds()
    {
        List<SeamProblem> problems = Check(Loft(hole: 4f), Well());
        Assert.Contains(problems, p => p.Rule == SeamRule.Opening && p.Seam == "Loft:Hole | Well:Roof" && p.Detail.Contains("4 m wide in Loft", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Rule == SeamRule.Solids && p.Detail.Contains("open in Loft but solid in Well", StringComparison.Ordinal));
    }

    [Fact]
    public void WaterEndingAtTheSeamFails()
    {
        // The well flooded up through its roof hole, the loft dry above it.
        var flood = new List<(string, Box)> { ("Flood", new Box(-10f, 0f, 10f, 11f)) };
        Assert.Contains(Check(Loft(), Well(waters: flood)), p => p.Rule == SeamRule.Solids && p.Detail.Contains("open in Loft but water in Well", StringComparison.Ordinal));
    }

    [Fact]
    public void WaterOverAHoleNeedsWaterOrFloorBeyondIt()
    {
        // A pool on the loft's floor filling its hole, over the well's dry shaft.
        var pool = new List<(string, Box)> { ("Pool", new Box(-10f, -1f, 10f, 2f)) };
        Assert.Contains(Check(Loft(waters: pool), Well()), p => p.Rule == SeamRule.Water && p.Seam == "Loft:Hole | Well:Roof");

        // The same pool over a well flooded to meet it: the water continues.
        var flood = new List<(string, Box)> { ("Flood", new Box(-10f, 0f, 10f, 12f)) };
        Assert.DoesNotContain(Check(Loft(waters: pool), Well(waters: flood)), p => p.Rule == SeamRule.Water);
    }

    [Fact]
    public void WaterWithoutAFloorFails()
    {
        // The well's water hangs a meter over its floor.
        var hanging = new List<(string, Box)> { ("Hanging", new Box(-10f, 1f, 10f, 4f)) };
        SeamProblem bare = Assert.Single(Check(Loft(), Well(waters: hanging)), p => p.Rule == SeamRule.Water);
        Assert.Equal("Well", bare.Seam);
    }

    [Fact]
    public void ALiftThroughAHoleHasItsCarOnTheOtherSide()
    {
        // The well's lift rises from its floor to just under its roof hole.
        var lift = new List<LiftPath> { new(new Box(-1f, -0.25f, 1f, 0f), 7.5f) };
        Assert.Contains(Check(Loft(), Well(lifts: lift)), p => p.Rule == SeamRule.Lift && p.Detail.Contains("Loft has no car", StringComparison.Ordinal));

        // A car in the loft, as wide and in line, standing in its hole.
        var car = new List<LiftPath> { new(new Box(-1f, -0.25f, 1f, 0f), 0f) };
        Assert.DoesNotContain(Check(Loft(lifts: car), Well(lifts: lift)), p => p.Rule == SeamRule.Lift);
    }

    [Fact]
    public void EachProblemIsALineNamingItsSeamAndRule()
    {
        var seams = new SeamCheck();
        seams.AddRoom("Loft", Loft(hole: 4f));
        seams.AddRoom("Well", Well());
        seams.AddWay("Loft", "Hole", "Well", "Roof");
        Assert.Contains(seams.Check(Reach), l => l.StartsWith("seam Loft:Hole | Well:Roof (Opening): ", StringComparison.Ordinal));
    }
}
