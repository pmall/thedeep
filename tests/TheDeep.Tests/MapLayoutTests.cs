using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class MapLayoutTests
{
    private static readonly Dictionary<string, (Vector2, Vector2)> Bounds = new()
    {
        ["A"] = (new Vector2(-2f, -2f), new Vector2(38f, 22f)),
        ["B"] = (new Vector2(-2f, -2f), new Vector2(66f, 34f)),
        ["C"] = (new Vector2(-2f, -2f), new Vector2(20f, 20f)),
    };

    [Fact]
    public void RoomsArePlacedFromTheirDoorways()
    {
        var links = new[] { new MapLink("A", "East", new Vector2(37f, 0f), "B", "West", new Vector2(1f, 0f)) };
        MapLayout.Result r = MapLayout.Place("B", links, Bounds, 6f);
        Assert.Empty(r.Problems);
        Assert.Equal(new Vector2(-36f, 0f), r.Offsets["A"]);
    }

    [Fact]
    public void ALoopThatDoesNotLineUpIsReported()
    {
        var links = new[]
        {
            new MapLink("A", "East", new Vector2(37f, 0f), "B", "West", new Vector2(1f, 0f)),
            new MapLink("B", "Up", new Vector2(10f, 33f), "C", "Down", new Vector2(5f, 0f)),
            new MapLink("C", "West", new Vector2(0f, 10f), "A", "Roof", new Vector2(20f, 21f)), // should be (x, y) consistent; it isn't
        };
        MapLayout.Result r = MapLayout.Place("A", links, Bounds, 6f);
        Assert.Contains(r.Problems, p => p.Contains("don't line up"));
    }

    [Fact]
    public void OverlappingRoomsAreReported()
    {
        var links = new[] { new MapLink("A", "Hatch", new Vector2(20f, 0f), "C", "Top", new Vector2(10f, 15f)) };
        MapLayout.Result r = MapLayout.Place("A", links, Bounds, 6f);
        Assert.Contains(r.Problems, p => p.Contains("overlap"));
    }

    [Fact]
    public void ASharedDoorwayMarginIsFine()
    {
        // B's west wall (x -2..0) and A's east wall overlap by 4 m in X at the doorway.
        var links = new[] { new MapLink("A", "East", new Vector2(37f, 0f), "B", "West", new Vector2(1f, 0f)) };
        Assert.Empty(MapLayout.Place("A", links, Bounds, 6f).Problems);
    }
}
