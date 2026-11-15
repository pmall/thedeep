using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class ExitMathTests
{
    [Fact]
    public void RoomsArePlacedFromTheExitsCrossed()
    {
        // East gate at x 63 of the first room leads to a west gate at x 1 of the next: that room starts 62 m east.
        Vector2 offset = ExitMath.RoomOffset(Vector2.Zero, new Vector2(63f, 26f), new Vector2(1f, 0f));
        Assert.Equal(new Vector2(62f, 26f), offset);
    }
}
