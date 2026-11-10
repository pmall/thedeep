using System;
using System.Numerics;
using TheDeep;
using Xunit;

namespace TheDeep.Tests;

public class ShaftBeamTests
{
    private static readonly Vector3 Down = -Vector3.UnitY;

    [Fact]
    public void ABeamFallsOntoTheFirstSolidUnderIt()
    {
        SolidBox[] solids =
        {
            SolidBox.Around(new Vector3(0f, 0f, 0f), new Vector3(10f, 2f, 4f)), // a floor, top at 1
            SolidBox.Around(new Vector3(0f, -10f, 0f), new Vector3(10f, 2f, 4f)), // a floor under it
        };
        Assert.Equal(19f, ShaftBeam.Reach(new Vector3(0f, 20f, 0f), Down, 80f, -50f, solids), 3);
    }

    [Fact]
    public void ItPassesBehindASolidButNotInFrontOfIt()
    {
        SolidBox[] ceiling = { SolidBox.Around(new Vector3(0f, 10f, 0f), new Vector3(10f, 2f, 4f)) };
        Assert.Equal(80f, ShaftBeam.Reach(new Vector3(0f, 20f, -5f), Down, 80f, -100f, ceiling), 3); // behind its back face
        Assert.Equal(9f, ShaftBeam.Reach(new Vector3(0f, 20f, 6f), Down, 80f, -100f, ceiling), 3); // in front: the solid runs toward the camera
    }

    [Fact]
    public void ItEndsUnderTheRoomsViewAndIgnoresTheSlabItShinesFrom()
    {
        SolidBox[] slab = { SolidBox.Around(new Vector3(0f, 20f, 0f), new Vector3(4f, 4f, 4f)) };
        Assert.Equal(25f, ShaftBeam.Reach(new Vector3(0f, 20f, 0f), Down, 80f, -5f, slab), 3);
    }

    [Fact]
    public void TheStripFacesTheCameraWidensWithTheConeAndStaysInFrontOfTheBackWall()
    {
        var origin = new Vector3(0f, 40f, -12f);
        var direction = Vector3.Normalize(new Vector3(0.3f, -1f, 0.3f));
        BeamStrip strip = ShaftBeam.Build(origin, direction, MathF.PI / 18f, 30f, 1.5f, Vector3.UnitZ, -4f);

        Assert.Equal((ShaftBeam.Rows + 1) * 2, strip.Positions.Length);
        Assert.Equal(new Vector2(30f, 1.5f), strip.Shape);
        for (int row = 0; row <= ShaftBeam.Rows; row++)
        {
            Vector3 left = strip.Positions[row * 2], right = strip.Positions[row * 2 + 1];
            Assert.Equal(left.Z, right.Z, 4); // the width runs across the view, not in depth
            Assert.True(origin.Z + left.Z >= -4.001f, "never behind the deepest plane");
        }

        float first = Vector3.Distance(strip.Positions[0], strip.Positions[1]);
        float last = Vector3.Distance(strip.Positions[^2], strip.Positions[^1]);
        Assert.Equal(2f * 30f * MathF.Tan(MathF.PI / 18f) * 1.5f, last, 3);
        Assert.True(first < 1f, "a narrow ray at the light");
        Assert.Equal(1f, strip.Uvs[^1].Y);
        Assert.Equal(-1f, strip.Uvs[0].X);
    }
}
