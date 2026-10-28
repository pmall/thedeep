using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace TheDeep.Tests;

public class MapDraftTests
{
    private readonly ITestOutputHelper _output;

    public MapDraftTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string Small = """
        start A
        room A Flooded
        room B Flooded   # a comment
        room C Frozen
        way A <-> B
        way B -> C : MagGrip | Harpoon
        way C -> A
        relic B MagGrip
        boss C Warden
        milestone B Third
        goal C
        """;

    [Fact]
    public void ParsesStatements()
    {
        MapDraft d = MapDraft.Parse(Small);
        Assert.Equal("A", d.Graph.Start);
        Assert.Equal(new[] { "Flooded", "Frozen" }, d.LayerOrder);
        Assert.Equal(4, d.Graph.Edges.Count);
        Assert.Equal("MagGrip | Harpoon", d.Graph.From("B").Single(e => e.To == "C").Requires.ToString());
        Assert.Equal(Ability.MagGrip, d.Graph.In("B").Single().Ability);
        Assert.Equal(new[] { ("C", "Warden") }, d.Bosses);
        Assert.Equal(new[] { "C" }, d.Graph.Goals);
    }

    [Theory]
    [InlineData("start A\nroom A Flooded\nway A -> Z", "never declared")]
    [InlineData("start A\nroom A Flooded\nroom A Frozen", "declared twice")]
    [InlineData("start A\nroom A Flooded\nrelic A Wings", "unknown ability")]
    [InlineData("start A\nroom A Flooded\nwander A", "unknown statement")]
    [InlineData("room A Flooded", "no start room")]
    [InlineData("start A\nroom A Flooded\nway A => A", "line 3")]
    public void RejectsMistakes(string text, string message)
    {
        var e = Assert.Throws<FormatException>(() => MapDraft.Parse(text));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void ReadsOnlyMapBlocksOfMarkdown()
    {
        string md = "# Title\nstart Nowhere\n```map\nstart A\nroom A Flooded\n```\n```\nroom X Frozen\n```\n```map\nway A -> A\n```\n";
        MapDraft d = MapDraft.FromMarkdown(md);
        Assert.Equal("A", d.Graph.Start);
        Assert.Single(d.Layers);
        Assert.Single(d.Graph.Edges);

        var e = Assert.Throws<FormatException>(() => MapDraft.FromMarkdown("text\n```map\nstart A\nroom A Flooded\nbogus\n```"));
        Assert.Contains("line 5", e.Message);
    }

    [Fact]
    public void AMilestoneARouteCanSkipIsAProblem()
    {
        MapDraft d = MapDraft.Parse(Small + "\nway A -> C");
        Assert.Contains(d.Check().Problems, p => p.Contains("without passing B"));
        Assert.DoesNotContain(MapDraft.Parse(Small).Check().Problems, p => p.Contains("milestone"));
    }

    [Fact]
    public void MilestonesOutOfOrderAreAProblem()
    {
        MapDraft d = MapDraft.Parse(Small + "\nmilestone A Second");
        Assert.Contains(d.Check().Problems, p => p.Contains("Second in A can be reached before Third"));
    }

    [Fact]
    public void AnAvoidedRoomCutsTheWay()
    {
        MapGraph g = MapDraft.Parse(Small).Graph;
        Assert.True(MapSolver.Reaches(g, new[] { "C" }, avoiding: "Nowhere"));
        Assert.False(MapSolver.Reaches(g, new[] { "C" }, avoiding: "B"));
        Assert.False(MapSolver.Reaches(g, new[] { "B" }, avoiding: "A"));
    }

    [Fact]
    public void DifferencesFromTheBuiltRooms()
    {
        MapDraft d = MapDraft.Parse(Small);
        var built = new MapGraph { Start = "A" };
        built.AddEdge(new MapEdge("A", "East", "B", "West", Requirement.None));
        built.AddEdge(new MapEdge("B", "West", "A", "East", Requirement.None));
        built.AddItem(new MapItem("B", "Relic", Ability.MagGrip));
        var sealedB = new[] { ("B", Requirement.Parse("Harpoon | MagGrip")) };

        Assert.Empty(d.Differences(built, sealedB));

        // The scene's sealed way needs Drill; the draft's needs Mag-grip or Harpoon.
        var problems = d.Differences(built, new[] { ("B", Requirement.Parse("Drill")) });
        Assert.Contains("B: the scene has a sealed way (not built) : Drill, the draft doesn't", problems);
        Assert.Contains("B: the draft has a sealed way (not built) : MagGrip | Harpoon, the scene doesn't", problems);

        built.AddRoom("D");
        Assert.Contains("D is built but not in the draft", d.Differences(built, sealedB));
    }

    /// <summary>The whole game's draft (docs/map-draft.md) meets every target.</summary>
    [Fact]
    public void TheWholeGameDraftHoldsUp()
    {
        MapDraft d = MapDraft.FromMarkdown(File.ReadAllText(Path.Combine(RepoRoot(), "docs", "map-draft.md")));
        (MapReport report, var problems) = d.Check();
        _output.WriteLine(d.Summary(report));
        foreach (string p in problems)
        {
            _output.WriteLine(p);
        }

        Assert.Empty(problems);
        Assert.Equal(new[] { "Flooded", "Frozen", "Volcanic", "Core" }, d.LayerOrder);

        // The design's rule (docs/map-draft.md, "Order freedom"): Mag-grip or Harpoon first, Heat plating after Drill or Ballast.
        Assert.All(report.Orders, o => Assert.Contains(o[0], new[] { Ability.MagGrip, Ability.Harpoon }));
        Assert.All(report.Orders, o => Assert.True(Array.IndexOf(o, Ability.HeatPlating) > Math.Min(Array.IndexOf(o, Ability.Drill), Array.IndexOf(o, Ability.Ballast))));
        Assert.Equal(32, report.Orders.Count);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("no AGENTS.md above the test binaries");
    }
}
