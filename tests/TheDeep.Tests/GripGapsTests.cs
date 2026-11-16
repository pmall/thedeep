using Xunit;

namespace TheDeep.Tests;

public class GripGapsTests
{
    private const float Body = 0.8f;

    [Fact]
    public void NarrowGapBetweenTopsFails()
    {
        var metal = new[] { new Box(0, 0, 4, 1), new Box(4.5f, 0, 8, 1) };
        Assert.Single(GripGaps.Problems(metal, Body));
    }

    [Fact]
    public void NarrowGapBetweenWallFacesFails()
    {
        var metal = new[] { new Box(0, 0, 1, 4), new Box(0, 4.5f, 1, 8) };
        Assert.Single(GripGaps.Problems(metal, Body));
    }

    [Fact]
    public void TouchingSolidsPass()
    {
        var metal = new[] { new Box(0, 0, 4, 1), new Box(4, 0, 8, 1) };
        Assert.Empty(GripGaps.Problems(metal, Body));
    }

    [Fact]
    public void WideGapPasses()
    {
        var metal = new[] { new Box(0, 0, 4, 1), new Box(4 + GripGaps.Widest(Body) + 0.1f, 0, 12, 1) };
        Assert.Empty(GripGaps.Problems(metal, Body));
    }

    [Fact]
    public void DifferentPlanesPass()
    {
        Assert.Empty(GripGaps.Problems(new[] { new Box(0, 0, 4, 1), new Box(4.5f, 0.5f, 8, 3) }, Body));
    }
}
