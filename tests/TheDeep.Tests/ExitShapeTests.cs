using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class ExitShapeTests
{
    private static readonly PlannerLimits Limits = TestTuning.Planner;

    [Fact]
    public void AnExitIsEnteredAsTravelPlacesTheBody()
    {
        // Through a doorway standing; up through a floor's hatch rising at least the climb's speed, steered its arrival side; down
        // through a roof's hatch falling.
        var door = new ExitShape("East", new Threshold(ExitSide.East, new Vector2(9.5f, 0f), new Vector2(1f, 3f)), "", 1, false);
        var floor = new ExitShape("Down", new Threshold(ExitSide.Down, new Vector2(0f, -1f), new Vector2(2f, 1f)), "", -1, false);
        var roof = new ExitShape("Up", new Threshold(ExitSide.Up, new Vector2(0f, 8f), new Vector2(2f, 1f)), "", 1, false);
        var solids = new List<Box>();
        Assert.Equal((Vector2.Zero, 0), (door.Entry(Limits, solids).Velocity, door.Entry(Limits, solids).Steer));
        Assert.Equal((new Vector2(0f, Limits.Rise), -1), (floor.Entry(Limits, solids).Velocity, floor.Entry(Limits, solids).Steer));
        Assert.Equal((new Vector2(0f, -ExitShape.FallInSpeed), 0), (roof.Entry(Limits, solids).Velocity, roof.Entry(Limits, solids).Steer));
        Assert.Equal(door.Threshold.Arrival(TestTuning.Body, solids), door.Entry(Limits, solids).At);
    }
}
