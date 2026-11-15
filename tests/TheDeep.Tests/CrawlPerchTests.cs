using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class CrawlPerchTests
{
    private static PerchBox Box(float x0, float y0, float x1, float y1, bool metal = true) => new(new Vector2(x0, y0), new Vector2(x1, y1), metal);

    [Fact]
    public void ChoosesTheNearestMetalBoxWithinReach()
    {
        PerchBox[] boxes = { Box(0f, 0f, 4f, 1f, metal: false), Box(10f, 0f, 14f, 1f), Box(2f, 1f, 3f, 2f) };
        Assert.Equal(2, CrawlPerch.Nearest(boxes, new Vector2(2.5f, 1.2f), 1.5f));
        Assert.Equal(1, CrawlPerch.Nearest(boxes, new Vector2(9f, 0.5f), 1.5f));
        Assert.Equal(-1, CrawlPerch.Nearest(boxes, new Vector2(6f, 0.5f), 1.5f));
    }

    [Fact]
    public void ATieGoesToTheLaterBox()
    {
        PerchBox[] boxes = { Box(0f, 0f, 1f, 1f), Box(3f, 0f, 4f, 1f) };
        Assert.Equal(1, CrawlPerch.Nearest(boxes, new Vector2(2f, 0.5f), 1.5f));
    }

    [Fact]
    public void ProbesSitJustOffEachFaceAndShortOfItsCorners()
    {
        PerchBox box = Box(0f, 0f, 4f, 2f);
        FaceProbe bottom = CrawlPerch.Probe(box, 0);
        Assert.Equal(new Vector2(2f, -CrawlPerch.FaceGap), bottom.Center);
        Assert.Equal(new Vector2(4f - CrawlPerch.CornerInset, CrawlPerch.FaceGap), bottom.Size);
        FaceProbe right = CrawlPerch.Probe(box, 1);
        Assert.Equal(new Vector2(4f + CrawlPerch.FaceGap, 1f), right.Center);
        Assert.Equal(new Vector2(CrawlPerch.FaceGap, 2f - CrawlPerch.CornerInset), right.Size);
        Assert.Equal(new Vector2(2f, 2f + CrawlPerch.FaceGap), CrawlPerch.Probe(box, 2).Center);
        Assert.Equal(new Vector2(-CrawlPerch.FaceGap, 1f), CrawlPerch.Probe(box, 3).Center);
    }

    [Fact]
    public void NoFreeFaceIsNoPath()
    {
        Assert.Null(CrawlPerch.Path(Box(0f, 0f, 1f, 1f), new[] { false, false, false, false }));
        Assert.NotNull(CrawlPerch.Path(Box(0f, 0f, 1f, 1f), new[] { false, false, true, false }));
    }
}
