using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class CameraFollowTests
{
    private const float Dt = 1f / 60f;

    private static readonly FollowLimits L = new(LookAhead: 2.5f, LookAheadMinSpeed: 1f, LookAheadTime: 0.45f, HorizontalTime: 0.2f,
        VerticalTime: 0.24f, RiseBand: 2.2f, FallLead: 4.5f, FallLeadFrom: 8f, FallLeadFull: 18f, LeashX: 4.5f, LeashY: 2.5f, KickTime: 0.06f);

    private static readonly ViewBounds Open = new(new Vector2(-1000f), new Vector2(1000f), new Vector2(10f, 6f));

    private static CameraFollow At(Vector2 target)
    {
        var follow = new CameraFollow(L);
        follow.Snap(target, 1, Open);
        return follow;
    }

    /// <summary>Steps <paramref name="seconds"/> with the target held still at <paramref name="target"/>.</summary>
    private static void Hold(CameraFollow follow, Vector2 target, CameraFooting footing, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt)
        {
            follow.Step(Dt, target, Vector2.Zero, footing, Open);
        }
    }

    [Fact]
    public void SnapLooksAheadOfTheFacing()
    {
        var follow = new CameraFollow(L);
        follow.Snap(new Vector2(3f, 1f), -1, Open);
        Assert.Equal(new Vector2(3f - L.LookAhead, 1f), follow.Focus);
    }

    [Fact]
    public void AHopInsideTheBandLeavesTheHeightAlone()
    {
        CameraFollow follow = At(Vector2.Zero);
        for (float y = 0f; y < L.RiseBand - 0.2f; y += 0.05f)
        {
            follow.Step(Dt, new Vector2(0f, y), new Vector2(0f, 5f), CameraFooting.Air, Open);
        }

        Assert.Equal(0f, follow.Focus.Y, 3);
    }

    [Fact]
    public void ARiseOutOfTheBandIsFollowed()
    {
        CameraFollow follow = At(Vector2.Zero);
        Hold(follow, new Vector2(0f, 5f), CameraFooting.Air, 2f);
        Assert.Equal(5f - L.RiseBand, follow.Focus.Y, 2);
    }

    [Fact]
    public void LandingHigherSettlesOnTheNewFloor()
    {
        CameraFollow follow = At(Vector2.Zero);
        Hold(follow, new Vector2(0f, 2f), CameraFooting.Air, 0.5f);
        Assert.Equal(0f, follow.Focus.Y, 3);
        Hold(follow, new Vector2(0f, 2f), CameraFooting.Ground, 2f);
        Assert.Equal(2f, follow.Focus.Y, 2);
    }

    [Fact]
    public void AFastFallIsLedFromBelowAndLeashed()
    {
        CameraFollow follow = At(Vector2.Zero);
        float y = 0f;
        for (int i = 0; i < 60; i++)
        {
            y -= 20f * Dt;
            follow.Step(Dt, new Vector2(0f, y), new Vector2(0f, -20f), CameraFooting.Air, Open);
            Assert.True(follow.Focus.Y - y <= L.LeashY + 1e-3f, "the leash holds the body near where the camera wants it");
        }

        // At full fall speed it looks under the body, lagging by no more than the leash.
        Assert.True(follow.Focus.Y < y - 1f, $"the camera looks below a fast fall ({follow.Focus.Y - y} m)");
    }

    [Fact]
    public void TheEaseStartsGentlyAndNeverOvershoots()
    {
        CameraFollow follow = At(Vector2.Zero);
        var to = new Vector2(0f, 4f); // a landing on a higher floor: the target jumps, the body is still
        follow.Step(Dt, to, Vector2.Zero, CameraFooting.Ground, Open);
        float first = follow.Focus.Y;
        // An exponential follow of the same lag would move 4 * (1 - e^(-5/60)) ≈ 0.32 m at once: the spring starts far softer.
        Assert.True(first < 0.1f, $"first frame moved {first} m");
        float last = first;
        for (int i = 0; i < 180; i++)
        {
            follow.Step(Dt, to, Vector2.Zero, CameraFooting.Ground, Open);
            Assert.True(follow.Focus.Y >= last - 1e-5f && follow.Focus.Y <= 4f + 1e-4f, "monotonic, no overshoot");
            last = follow.Focus.Y;
        }

        Assert.Equal(4f, last, 2);
    }

    [Fact]
    public void TheLookAheadSwingsToTheWayOfTravel()
    {
        CameraFollow follow = At(Vector2.Zero);
        for (int i = 0; i < 180; i++)
        {
            follow.Step(Dt, Vector2.Zero, new Vector2(-7f, 0f), CameraFooting.Ground, Open);
        }

        Assert.Equal(-L.LookAhead, follow.Focus.X, 2);
    }

    [Fact]
    public void TheViewStaysInsideTheRoom()
    {
        var room = new ViewBounds(new Vector2(0f, 0f), new Vector2(40f, 20f), new Vector2(10f, 6f));
        var follow = new CameraFollow(L);
        follow.Snap(new Vector2(1f, 1f), 1, room);
        Assert.Equal(new Vector2(10f, 6f), follow.Focus);
        for (int i = 0; i < 120; i++)
        {
            follow.Step(Dt, new Vector2(39f, 19f), Vector2.Zero, CameraFooting.Ground, room);
            Assert.InRange(follow.Focus.X, 10f, 30f);
            Assert.InRange(follow.Focus.Y, 6f, 14f);
        }
    }

    [Fact]
    public void AKickThrowsTheViewThenEasesBack()
    {
        CameraFollow follow = At(Vector2.Zero);
        follow.Kick(new Vector2(0.1f, 0f));
        follow.Kick(new Vector2(0.05f, 0f)); // a weaker one under way changes nothing
        Assert.Equal(0.1f, follow.KickOffset.X);
        Hold(follow, Vector2.Zero, CameraFooting.Ground, 0.4f);
        Assert.Equal(0f, follow.KickOffset.X, 3);
        Assert.Equal(L.LookAhead, follow.Focus.X, 3); // the follow itself never moved
    }

    [Theory]
    [InlineData(MotionMode.Grip, false, false, CameraFooting.Tracked)]
    [InlineData(MotionMode.Zip, false, true, CameraFooting.Tracked)]
    [InlineData(MotionMode.Swim, false, true, CameraFooting.Tracked)]
    [InlineData(MotionMode.Ground, true, true, CameraFooting.Ground)]
    [InlineData(MotionMode.Dash, true, true, CameraFooting.Ground)]
    [InlineData(MotionMode.Air, false, true, CameraFooting.Air)]
    [InlineData(MotionMode.Dive, false, true, CameraFooting.Air)]
    public void FootingFollowsTheMotion(MotionMode motion, bool onFloor, bool upright, CameraFooting expected)
    {
        Assert.Equal(expected, CameraFollow.FootingOf(motion, onFloor, upright));
    }
}
