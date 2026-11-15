using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class StageSpecTests
{
    private static readonly StageSpec Spec = StageSpec.Default;

    [Fact]
    public void TheFrameBelowStandsUnderTheFloorAcrossTheWidthAndItsMargins()
    {
        FrameBand band = Spec.Frame(new Vector2(-30f, -10f), new Vector2(30f, 20f), above: false);

        Assert.Equal(new Vector2(-34f, -14f), band.AreaMin);
        Assert.Equal(new Vector2(34f, -13f), band.AreaMax);
        Assert.Equal(7, band.Count); // 68 m / 9.5 m = 7.2
    }

    [Fact]
    public void TheFrameAboveHangsOverTheCeiling()
    {
        FrameBand band = Spec.Frame(new Vector2(0f, 0f), new Vector2(40f, 12f), above: true);

        Assert.Equal(new Vector2(-4f, 14f), band.AreaMin);
        Assert.Equal(new Vector2(44f, 15f), band.AreaMax);
        Assert.Equal(3, band.Count); // 48 m / 15 m = 3.2
    }

    [Fact]
    public void ANarrowRoomStillHasOneSilhouette()
    {
        Assert.Equal(1, Spec.Frame(Vector2.Zero, new Vector2(1f, 5f), above: true).Count);
    }

    [Fact]
    public void TheSeedsOfTheStagePiecesStayApart()
    {
        Assert.Equal(44, Spec.Seed(1, Spec.FrameBelowSalt));
        Assert.Equal(48, Spec.Seed(1, Spec.FrameAboveSalt));
        Assert.Equal(18, Spec.Seed(1, Spec.ArchitectureSalt));
    }

    [Fact]
    public void TheViewIsWiderThanTheCameraSees()
    {
        float exact = CameraMath.HalfWidthAtDistance(1f, 30f, Spec.WideAspect);

        Assert.True(exact * Spec.ViewMargin > exact);
        Assert.Equal(CameraMath.HalfHeightAtDistance(1f, 30f) * Spec.WideAspect, exact, 5);
    }
}
