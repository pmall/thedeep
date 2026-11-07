using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TheDeep.Tests;

public class MapDrawingTests
{
    private const string Network = """
        start A
        goal D
        room A Top
        room B Top
        room C Low
        room D Low
        room S Low
        way A <-> B
        way B -> C : MagGrip
        way C -> B
        way C <-> D : StrongroomKey
        way C <-> S
        hidden C S
        relic B MagGrip
        item C StrongroomKey
        """;

    [Fact]
    public void DrawsEveryRoomOnceAndEveryWayOnce()
    {
        MapDraft draft = MapDraft.Parse(Network);
        XDocument svg = XDocument.Parse(MapDrawing.Svg(draft, null, "test"));
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Equal("svg", svg.Root!.Name.LocalName);

        var rooms = svg.Descendants(ns + "g").Where(g => (string?)g.Attribute("class") == "room").Select(g => (string)g.Attribute("data-room")!).ToList();
        Assert.Equal(draft.Graph.Rooms.OrderBy(r => r), rooms.OrderBy(r => r));

        var edges = svg.Descendants(ns + "g").Where(g => (string?)g.Attribute("class") == "way")
            .SelectMany(g => ((string)g.Attribute("data-edges")!).Split(',').Select(int.Parse)).OrderBy(i => i).ToList();
        Assert.Equal(Enumerable.Range(0, draft.Graph.Edges.Count), edges);
    }

    [Fact]
    public void MarksOneWayHiddenAndAbbreviatedWays()
    {
        string svg = MapDrawing.Svg(MapDraft.Parse(Network), null, "test");
        Assert.Contains("marker-end", svg);
        Assert.Contains("stroke-dasharray", svg);
        Assert.Contains(">SK<", svg);
        Assert.Contains("SK = StrongroomKey", svg);
    }

    [Theory]
    [InlineData("prototype", "map-draft.md")]
    [InlineData("sequel", "network.md")]
    public void DrawsEachWorld(string world, string network)
    {
        string dir = Path.Combine(RepoRoot(), "worlds", world);
        MapDraft d = MapDraft.FromMarkdown(File.ReadAllText(Path.Combine(dir, network)));
        RoomTable t = RoomTable.Parse(File.ReadAllText(Path.Combine(dir, "rooms.tsv")));
        XDocument svg = XDocument.Parse(MapDrawing.Svg(d, t, world));
        Assert.Equal(d.Graph.Rooms.Count, svg.Descendants().Count(e => (string?)e.Attribute("class") == "room"));
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
