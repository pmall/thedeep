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
        XElement root = Assert.IsType<XElement>(svg.Root);
        Assert.Equal("svg", root.Name.LocalName);

        var rooms = svg.Descendants(ns + "g").Where(g => (string?)g.Attribute("class") == "room").Select(g => Assert.IsType<XAttribute>(g.Attribute("data-room")).Value).ToList();
        Assert.Equal(draft.Graph.Rooms.OrderBy(r => r), rooms.OrderBy(r => r));

        var edges = svg.Descendants(ns + "g").Where(g => (string?)g.Attribute("class") == "way")
            .SelectMany(g => Assert.IsType<XAttribute>(g.Attribute("data-edges")).Value.Split(',').Select(int.Parse)).OrderBy(i => i).ToList();
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
    [InlineData("prototype")]
    [InlineData("sequel")]
    public void DrawsEachWorld(string world)
    {
        MapDraft d = MapDraft.FromMarkdown(File.ReadAllText(RepoPaths.Network(world)));
        RoomTable t = RoomTable.Parse(File.ReadAllText(RepoPaths.Rooms(world)));
        XDocument svg = XDocument.Parse(MapDrawing.Svg(d, t, world));
        Assert.Equal(d.Graph.Rooms.Count, svg.Descendants().Count(e => (string?)e.Attribute("class") == "room"));
    }
}
