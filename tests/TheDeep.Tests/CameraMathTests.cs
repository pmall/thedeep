using Xunit;

namespace TheDeep.Tests;

public class CameraMathTests
{
    [Theory]
    [InlineData(5f, 3f, 0f, 20f, 5f)]     // inside: unchanged
    [InlineData(1f, 3f, 0f, 20f, 3f)]     // near min edge: pushed in
    [InlineData(19f, 3f, 0f, 20f, 17f)]   // near max edge: pushed in
    [InlineData(1f, 15f, 0f, 20f, 10f)]   // room smaller than view: centered
    public void ClampAxis_keeps_the_view_inside_bounds(float center, float half, float min, float max, float expected)
    {
        Assert.Equal(expected, CameraMath.ClampAxis(center, half, min, max), 5);
    }

    [Fact]
    public void HalfHeight_matches_fov()
    {
        // 90° vertical FOV: half height equals distance.
        Assert.Equal(10f, CameraMath.HalfHeightAtDistance(10f, 90f), 4);
    }
}
