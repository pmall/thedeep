using System;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class SpinSweepTests
{
    /// <summary>A wheel of radius 5 and 1 m deep facing the camera (its axis along Z), its center 4 m behind the play plane.</summary>
    private static readonly Vector3[] Wheel = SpinSweep.Corners(new Vector3(-5f, -5f, -4.5f), new Vector3(5f, 5f, -3.5f));

    [Fact]
    public void AWheelTurningInItsPlaneKeepsItsDepth()
    {
        (Vector3 min, Vector3 max) = SpinSweep.Bounds(Wheel, new Vector3(0f, 0f, -4f), Vector3.UnitZ);
        Assert.Equal(-4.5f, min.Z, 4);
        Assert.Equal(-3.5f, max.Z, 4);
    }

    [Fact]
    public void AWheelTurningRoundTheVerticalSweepsThroughThePlayPlane()
    {
        (Vector3 min, Vector3 max) = SpinSweep.Bounds(Wheel, new Vector3(0f, 0f, -4f), Vector3.UnitY);
        Assert.True(min.Z < -8.9f && max.Z > 0.9f, $"z {min.Z}..{max.Z}");
        Assert.Equal(-5f, min.Y, 4);
        Assert.Equal(5f, max.Y, 4);
    }

    [Fact]
    public void TheBoxHoldsEveryTurnedPose()
    {
        var pivot = new Vector3(1f, 2f, -6f);
        Vector3 axis = Vector3.Normalize(new Vector3(0.3f, 1f, 0.5f));
        Vector3[] box = SpinSweep.Corners(new Vector3(-2f, 0f, -8f), new Vector3(3f, 1f, -5f));
        (Vector3 min, Vector3 max) = SpinSweep.Bounds(box, pivot, axis);
        for (int step = 0; step < 360; step++)
        {
            var turn = Matrix4x4.CreateFromAxisAngle(axis, step * MathF.PI / 180f);
            foreach (Vector3 p in box.Select(c => Vector3.Transform(c - pivot, turn) + pivot))
            {
                Assert.True(p.X >= min.X - 1e-4f && p.Y >= min.Y - 1e-4f && p.Z >= min.Z - 1e-4f, $"{p} under {min}");
                Assert.True(p.X <= max.X + 1e-4f && p.Y <= max.Y + 1e-4f && p.Z <= max.Z + 1e-4f, $"{p} over {max}");
            }
        }
    }
}
