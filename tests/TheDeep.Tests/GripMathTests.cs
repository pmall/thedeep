using System;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class GripMathTests
{
    private static readonly Vector2 Up = new(0f, 1f);
    private static readonly Vector2 Down = new(0f, -1f);
    private static readonly Vector2 Right = new(1f, 0f);
    private static readonly Vector2 Left = new(-1f, 0f);

    private static void Near(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }

    [Fact]
    public void TangentIsTheBodysForward()
    {
        Near(Right, GripMath.Tangent(Up));
        Near(Down, GripMath.Tangent(Right)); // standing out to the right of a wall: forward is down
        Near(Up, GripMath.Tangent(Left));
        Near(Left, GripMath.Tangent(Down));
    }

    [Fact]
    public void AngleTurnsUpOntoTheNormal()
    {
        foreach (Vector2 n in new[] { Up, Right, Down, Left })
        {
            float a = GripMath.Angle(n);
            var rotatedUp = new Vector2(-MathF.Sin(a), MathF.Cos(a));
            Near(n, rotatedUp);
        }
    }

    [Fact]
    public void InputMovesTheWayItPoints()
    {
        // On a wall facing right (normal +X), pressing up climbs: velocity = tangent * along points up.
        float along = GripMath.Along(Up, Right);
        Near(Up, GripMath.Tangent(Right) * along);
        // Under a ceiling, pressing right moves right.
        Near(Right, GripMath.Tangent(Down) * GripMath.Along(Right, Down));
        Assert.True(GripMath.PullsAway(Down, Down));
        Assert.True(GripMath.PullsAway(Left, Left));
        Assert.False(GripMath.PullsAway(Up, Right));
    }

    [Fact]
    public void ClimbingAWallIntoACeilingTurnsUnderIt()
    {
        // Feet on a wall face at x = 0 (normal +X), 2 m below a ceiling at y = 10 (normal -Y).
        GripPose pose = GripMath.InnerCorner(new Vector2(0f, 8f), Right, new Vector2(0.3f, 10f), Down, 0.37f);
        Near(new Vector2(0.37f, 10f), pose.Feet);
        Near(Down, pose.Up);
        Near(Right, GripMath.TravelAfterInner(Right)); // then moving right, away from the wall
    }

    [Fact]
    public void WalkingDownAWallOntoTheFloorStandsUp()
    {
        GripPose pose = GripMath.InnerCorner(new Vector2(5f, 1f), Left, new Vector2(4.6f, 0f), Up, 0.37f);
        Near(new Vector2(4.63f, 0f), pose.Feet);
        Near(Up, pose.Up);
    }

    [Fact]
    public void TopOfAWallWrapsOntoTheTop()
    {
        // Feet on the right face (x = 4) of a block whose top is y = 6, moving up, now 0.25 m past the top.
        GripPose pose = GripMath.OuterCorner(new Vector2(4f, 6.25f), Right, Up, new Vector2(3.9f, 6f), 0.37f);
        Near(new Vector2(3.63f, 6f), pose.Feet);
        Near(Up, pose.Up);
        Near(Left, GripMath.TravelAfterOuter(Right)); // then walking inward over the top
    }

    [Fact]
    public void EndOfACeilingWrapsUpItsSide()
    {
        // Hanging under a block (bottom y = 9) moving right past its right face x = 12.
        GripPose pose = GripMath.OuterCorner(new Vector2(12.25f, 9f), Down, Right, new Vector2(12f, 9.1f), 0.37f);
        Near(new Vector2(12f, 9.37f), pose.Feet);
        Near(Right, pose.Up);
        Near(Up, GripMath.TravelAfterOuter(Down));
    }

    [Fact]
    public void AttachingAndLettingGoKeepTheBodyCenter()
    {
        const float center = 0.8f;
        var feet = new Vector2(2f, 0f);
        GripPose wall = GripMath.OntoWall(feet, center, 1.65f, Right);
        Near(new Vector2(1.65f, 0.8f), wall.Feet);
        Near(new Vector2(2.45f, 0f), GripMath.Upright(wall.Feet, wall.Up, center));

        GripPose ceiling = GripMath.OntoCeiling(new Vector2(3f, 8.4f), 10f);
        Near(new Vector2(3f, 10f), ceiling.Feet);
        Near(new Vector2(3f, 8.4f), GripMath.Upright(ceiling.Feet, ceiling.Up, center));
    }

    [Fact]
    public void SnapPicksTheNearestAxis()
    {
        Near(Right, GripMath.Snap(new Vector2(0.9f, 0.2f)));
        Near(Down, GripMath.Snap(new Vector2(0.1f, -0.99f)));
    }
}
