using System;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class PartsTests
{
    [Theory]
    [InlineData(0.2f, -0.6f, 1f)]
    [InlineData(-0.3f, -0.5f, 1f)]
    [InlineData(0.25f, -0.4f, -1f)]
    public void TwoBoneReachesItsTargetAndBendsTheRightWay(float x, float y, float bend)
    {
        var root = new Vector2(0f, 1f);
        var target = root + new Vector2(x, y);
        (float upper, float lower) = PlanarIk.TwoBone(root, target, 0.4f, 0.39f, bend);
        Vector2 middle = PlanarIk.End(root, upper, 0.4f);
        Vector2 end = PlanarIk.End(middle, lower, 0.39f);

        Assert.True(Vector2.Distance(target, end) < 1e-3f, $"ended at {end}, target {target}");
        // For a limb reaching down, a positive bend puts the middle joint forward (+X) of the root-target line.
        Vector2 d = target - root;
        float side = d.X * (middle.Y - root.Y) - d.Y * (middle.X - root.X);
        Assert.True(side * bend > 0f, "bends the wrong way");
    }

    [Fact]
    public void TwoBoneStraightensTowardAnOutOfReachTarget()
    {
        (float upper, float lower) = PlanarIk.TwoBone(Vector2.Zero, new Vector2(0f, -5f), 0.4f, 0.4f, 1f);
        Assert.True(MathF.Abs(upper) < 0.03f && MathF.Abs(lower) < 0.03f);
    }

    [Fact]
    public void ChainHangsAlongItsRestDirectionAndKeepsItsLinks()
    {
        var chain = new VerletChain(6, 0.1f) { RestDirection = new Vector3(0f, -1f, 0f) };
        chain.Reset(Vector3.Zero, Quaternion.Identity);
        for (int i = 0; i < 120; i++)
        {
            chain.Step(1f / 60f, Vector3.Zero, Quaternion.Identity);
        }

        Assert.True(Vector3.Distance(chain.Points[^1], new Vector3(0f, -0.6f, 0f)) < 0.01f);
        AssertLinks(chain);
    }

    [Fact]
    public void ChainTrailsBehindAMovingAnchor()
    {
        var chain = new VerletChain(6, 0.1f) { Drag = 3f, Stiffness = 1f };
        chain.Reset(Vector3.Zero, Quaternion.Identity);
        var anchor = Vector3.Zero;
        for (int i = 0; i < 60; i++)
        {
            anchor += new Vector3(7f / 60f, 0f, 0f);
            chain.Step(1f / 60f, anchor, Quaternion.Identity);
        }

        Assert.True(chain.Points[^1].X < anchor.X - 0.1f, "the tip streams behind");
        AssertLinks(chain);
    }

    [Fact]
    public void ChainStaysAboveItsFloor()
    {
        var chain = new VerletChain(8, 0.1f) { Floor = -0.3f };
        chain.Reset(Vector3.Zero, Quaternion.Identity);
        for (int i = 0; i < 60; i++)
        {
            chain.Step(1f / 60f, Vector3.Zero, Quaternion.Identity);
        }

        Assert.All(chain.Points, p => Assert.True(p.Y >= -0.3f - 1e-4f));
    }

    [Fact]
    public void PodIsClosedRoundedAndSpansItsLength()
    {
        var b = new MeshBuilder();
        PartShapes.Pod(b, -Vector3.UnitY, 0.4f, new[] { new Vector4(0f, 0f, 0f, 0f), new Vector4(0.5f, 0.12f, 0.1f, 0f), new Vector4(1f, 0f, 0f, 0f) }, 16, 12);
        MeshData mesh = b.Finish();

        AssertValidFrames(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(-0.4f, min.Y, 3);
        Assert.Equal(0f, max.Y, 3);
        Assert.InRange(max.X, 0.055f, 0.065f);
        Assert.InRange(max.Z, 0.045f, 0.055f);
        // Poles: the normals at the ends face along the axis.
        Assert.Contains(mesh.Normals, n => n.Y > 0.99f);
        Assert.Contains(mesh.Normals, n => n.Y < -0.99f);
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            Vector3 center = new(0f, mesh.Positions[i].Y, 0f);
            Vector3 outward = mesh.Positions[i] - center;
            if (outward.LengthSquared() > 1e-4f)
            {
                Assert.True(Vector3.Dot(outward, mesh.Normals[i]) > 0f, "normals point out");
            }
        }
    }

    [Fact]
    public void PodEndWithARadiusIsCapped()
    {
        var b = new MeshBuilder();
        PartShapes.Pod(b, Vector3.UnitY, 0.2f, new[] { new Vector4(0f, 0.1f, 0.1f, 0f), new Vector4(1f, 0.1f, 0.1f, 0f) }, 12, 4);
        MeshData mesh = b.Finish();
        AssertValidFrames(mesh);
        Assert.Contains(mesh.Normals, n => n.Y < -0.99f);
        Assert.Contains(mesh.Normals, n => n.Y > 0.99f);
    }

    [Fact]
    public void PlateTapersAndCurves()
    {
        var b = new MeshBuilder();
        PartShapes.Plate(b, 0.2f, 0.4f, 0.02f, 0.5f, 0.5f, 0.005f);
        MeshData mesh = b.Finish();
        AssertValidFrames(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.InRange(max.X - min.X, 0.19f, 0.21f);
        Assert.True(max.Z > 0.05f, "curves toward +Z");
        float tipWidth = mesh.Positions.Where(p => p.Y < min.Y + 0.01f).Max(p => MathF.Abs(p.X));
        Assert.InRange(tipWidth, 0.04f, 0.06f);
    }

    [Fact]
    public void ClosedRingWrapsWithoutEdgesAlongIt()
    {
        var b = new MeshBuilder();
        PartShapes.Ring(b, 0.1f, 0.12f, 0.04f, 0.01f, MathF.Tau, 24);
        MeshData mesh = b.Finish();
        AssertValidFrames(mesh);
        Assert.All(mesh.PatchSizes, s => Assert.True(s.X < 0f));
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.InRange(max.X, 0.1f, 0.11f);
        Assert.InRange(max.Z, 0.12f, 0.13f);
    }

    [Fact]
    public void ScaledSweepNarrowsTheProfile()
    {
        var b = new MeshBuilder();
        var path = SweepPath.Line(Vector3.Zero, new Vector3(0f, -1f, 0f), Vector3.UnitZ);
        b.Sweep(path, Profile.Rect(0.4f, 0.02f, 0f), scales: new[] { Vector2.One, new Vector2(0.25f, 1f) });
        MeshData mesh = b.Finish();
        float bottom = mesh.Positions.Where(p => p.Y < -0.99f).Max(p => p.X);
        Assert.Equal(0.05f, bottom, 3);
    }

    [Fact]
    public void CurledPodHooksForwardAndKeepsItsLength()
    {
        var b = new MeshBuilder();
        var stations = new[] { new Vector4(0f, 0.06f, 0.06f, 0f), new Vector4(1f, 0f, 0f, 0f) };
        PartShapes.Pod(b, -Vector3.UnitY, 0.5f, stations, 10, 16, MathF.PI * 0.5f);
        MeshData mesh = b.Finish();
        AssertValidFrames(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        // A quarter turn over 0.5 m: the tip ends about 2/π × 0.5 m forward and down.
        float r = 0.5f / (MathF.PI * 0.5f);
        Assert.InRange(max.X, r - 0.02f, r + 0.04f);
        Assert.InRange(min.Y, -r - 0.04f, -r + 0.02f);
    }

    [Fact]
    public void RotorHasItsBladesOutToItsRadius()
    {
        var b = new MeshBuilder();
        PartShapes.Rotor(b, 0.5f, 4, 0.1f, 0.2f, 0.06f, 0.05f);
        MeshData mesh = b.Finish();
        AssertValidFrames(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.InRange(max.X, 0.49f, 0.52f);
        Assert.InRange(min.Z, -0.52f, -0.49f);
        Assert.True(max.Y - min.Y < 0.1f, "flat");
    }

    [Fact]
    public void TreadRestsOnTheGroundAndWrapsAlongItsLoop()
    {
        var b = new MeshBuilder();
        PartShapes.Tread(b, 0.6f, 0.15f, 0.12f, 0.03f, 3);
        MeshData mesh = b.Finish();
        AssertValidFrames(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(0f, min.Y, 2);
        Assert.Equal(0.3f, max.Y, 2);
        Assert.Equal(0.9f, max.X - min.X, 2);
        Assert.Contains(mesh.PatchSizes, size => size.X < -1f);
    }

    [Fact]
    public void DrillPointsDown()
    {
        var b = new MeshBuilder();
        PartShapes.Drill(b, 0.4f, 0.08f, 2, 2.5f);
        MeshData mesh = b.Finish();
        AssertValidFrames(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(-0.4f, min.Y, 2);
        Assert.Equal(0f, max.Y, 3);
        float nearTip = mesh.Positions.Where(p => p.Y < -0.38f).Max(p => MathF.Abs(p.X));
        Assert.True(nearTip < 0.03f, "tapers to a point");
    }

    private static void AssertLinks(VerletChain chain)
    {
        for (int i = 1; i < chain.Points.Length; i++)
        {
            Assert.Equal(chain.LinkLength, Vector3.Distance(chain.Points[i], chain.Points[i - 1]), 3);
        }
    }

    private static void AssertValidFrames(MeshData mesh)
    {
        Assert.True(mesh.TriangleCount > 0);
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            Vector3 n = mesh.Normals[i];
            Vector4 t = mesh.Tangents[i];
            Assert.False(float.IsNaN(n.X + n.Y + n.Z + t.X + t.Y + t.Z), $"vertex {i} has NaN");
            Assert.Equal(1f, n.Length(), 3);
            Assert.True(MathF.Abs(Vector3.Dot(n, new Vector3(t.X, t.Y, t.Z))) < 1e-3f);
        }
    }
}
