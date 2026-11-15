using Xunit;

namespace TheDeep.Tests;

public class GridSnapTests
{
    [Theory]
    [InlineData(0.25f, 0.5f, true)]
    [InlineData(0f, 0.5f, false)]     // edges at -0.25 and 0.25
    [InlineData(60.75f, 7.5f, true)]   // edges at 57 and 64.5
    [InlineData(10f, 4f, true)]
    [InlineData(10.1f, 4f, false)]
    [InlineData(2.6f, 0.5f, false)]    // edges at 2.35 and 2.85
    public void BlockEdgesSnapToTheGrid(float center, float size, bool expected)
    {
        Assert.Equal(expected, GridSnap.EdgesOnGrid(center, size, GridSnap.BlockStep));
    }
}
