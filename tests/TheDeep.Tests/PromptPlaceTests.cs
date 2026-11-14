using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class PromptPlaceTests
{
    private static readonly Vector2 Canvas = new(1600f, 900f);
    private static readonly Vector2 Plate = new(140f, 56f);

    [Fact]
    public void APromptStandsCenteredOnItsPlace()
    {
        Assert.Equal(new Vector2(730f, 372f), PromptPlace.TopLeft(new Vector2(800f, 400f), Plate, 0, Canvas, 16f));
    }

    [Fact]
    public void APromptReachingEastHasItsWestEdgeOnItsPlace()
    {
        Assert.Equal(new Vector2(800f, 372f), PromptPlace.TopLeft(new Vector2(800f, 400f), Plate, 1, Canvas, 16f));
    }

    [Fact]
    public void APromptReachingWestHasItsEastEdgeOnItsPlace()
    {
        Assert.Equal(new Vector2(660f, 372f), PromptPlace.TopLeft(new Vector2(800f, 400f), Plate, -1, Canvas, 16f));
    }

    [Fact]
    public void APromptAtTheScreensEdgeIsKeptWholeOnIt()
    {
        Assert.Equal(new Vector2(16f, 16f), PromptPlace.TopLeft(new Vector2(20f, 10f), Plate, 0, Canvas, 16f));
        Assert.Equal(new Vector2(1600f - 16f - 140f, 900f - 16f - 56f), PromptPlace.TopLeft(new Vector2(1590f, 895f), Plate, 1, Canvas, 16f));
    }

    [Fact]
    public void APlateWiderThanTheCanvasIsCenteredOnIt()
    {
        Assert.Equal(new Vector2(-100f, 372f), PromptPlace.TopLeft(new Vector2(30f, 400f), new Vector2(1800f, 56f), 0, Canvas, 16f));
    }
}
