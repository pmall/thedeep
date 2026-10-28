using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class HarpoonAimTests
{
    private static int Pick(Vector2 aim, int facing, params Vector2[] anchors) =>
        HarpoonAim.Pick(Vector2.Zero, aim, facing, anchors, range: 9f, minDistance: 1.2f, clear: _ => true);

    [Fact]
    public void OutOfRangeOrTooNearIsNeverPicked()
    {
        Assert.Equal(-1, Pick(Vector2.Zero, 1, new Vector2(10f, 0f), new Vector2(0.5f, 0.5f)));
    }

    [Fact]
    public void WithoutInputAheadBeatsAboveBeatsBehind()
    {
        Vector2 behind = new(-3f, 1f), above = new(0f, 5f), ahead = new(7f, 2f);
        Assert.Equal(2, Pick(Vector2.Zero, 1, behind, above, ahead));
        Assert.Equal(1, Pick(Vector2.Zero, 1, behind, above));
        Assert.Equal(0, Pick(Vector2.Zero, 1, behind));
        Assert.Equal(0, Pick(Vector2.Zero, -1, behind, above, ahead));
    }

    [Fact]
    public void NearerWinsWithinAGroup()
    {
        Assert.Equal(1, Pick(Vector2.Zero, 1, new Vector2(8f, 1f), new Vector2(4f, 1f)));
    }

    [Fact]
    public void HeldInputAimsAndExcludesWhatIsOutsideItsCone()
    {
        Vector2 up = new(0f, 6f), ahead = new(6f, 0f);
        Assert.Equal(0, Pick(new Vector2(0f, 1f), 1, up, ahead));
        Assert.Equal(-1, Pick(new Vector2(0f, -1f), 1, up, ahead));
        Assert.Equal(1, Pick(new Vector2(1f, -1f), 1, up, ahead));
    }

    [Fact]
    public void BlockedSightAndTheLastAnchorAreSkipped()
    {
        var anchors = new[] { new Vector2(3f, 1f), new Vector2(6f, 1f) };
        Assert.Equal(1, HarpoonAim.Pick(Vector2.Zero, Vector2.Zero, 1, anchors, 9f, 1.2f, i => i != 0));
        Assert.Equal(1, HarpoonAim.Pick(Vector2.Zero, Vector2.Zero, 1, anchors, 9f, 1.2f, _ => true, skip: 0));
    }

    [Fact]
    public void ReleaseLiftsUnlessThePullWentDown()
    {
        Vector2 sideways = HarpoonAim.Release(new Vector2(1f, 0f), 17f, 0.5f, 6f);
        Assert.Equal(8.5f, sideways.X, 3);
        Assert.Equal(6f, sideways.Y, 3);
        Vector2 down = HarpoonAim.Release(new Vector2(0f, -1f), 17f, 0.5f, 6f);
        Assert.Equal(-8.5f, down.Y, 3);
    }
}
