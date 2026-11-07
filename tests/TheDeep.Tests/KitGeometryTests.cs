using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class KitGeometryTests
{
    private const float Epsilon = 1e-4f;

    [Fact]
    public void SharpBoxHasSixQuadsFittingItsSize()
    {
        var b = new MeshBuilder();
        b.Box(new Vector3(1f, 2f, 3f), new Vector3(4f, 2f, 1f), 0f);
        MeshData mesh = b.Finish();

        Assert.Equal(24, mesh.VertexCount);
        Assert.Equal(12, mesh.TriangleCount);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        AssertClose(new Vector3(-1f, 1f, 2.5f), min);
        AssertClose(new Vector3(3f, 3f, 3.5f), max);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.1f)]
    [InlineData(5f)] // clamped to fit
    public void BoxIsClosedFacesOutAndKeepsItsSize(float bevel)
    {
        var b = new MeshBuilder();
        var size = new Vector3(3f, 0.5f, 4f);
        b.Box(Vector3.Zero, size, bevel);
        MeshData mesh = b.Finish();

        AssertWatertight(mesh);
        AssertFrontFacesFollowNormals(mesh);
        AssertFramesValid(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        AssertClose(-size / 2f, min);
        AssertClose(size / 2f, max);
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            Assert.True(Vector3.Dot(mesh.Normals[i], mesh.Positions[i]) > 0f, "normals point out of the box");
        }
    }

    [Fact]
    public void BoxFacePatchesAreMeasuredFromTheBoxCorner()
    {
        var b = new MeshBuilder();
        var size = new Vector3(4f, 2f, 1f);
        b.Box(Vector3.Zero, size, 0.1f);
        MeshData mesh = b.Finish();

        // On the front face (+Z), U grows along +X and V along +Y, and the patch is the whole face.
        int front = Enumerable.Range(0, mesh.VertexCount)
            .First(i => mesh.Normals[i] == Vector3.UnitZ && mesh.Positions[i].X > 1.5f && mesh.Positions[i].Y > 0.5f);
        AssertClose(new Vector2(size.X, size.Y), mesh.PatchSizes[front]);
        AssertClose(new Vector2(3.9f, 1.9f), mesh.Uvs[front]);
        Vector4 t = mesh.Tangents[front];
        AssertClose(Vector3.UnitX, new Vector3(t.X, t.Y, t.Z));
        AssertClose(Vector3.UnitY, Vector3.Cross(mesh.Normals[front], new Vector3(t.X, t.Y, t.Z)) * t.W);

        // The top/front chamfer belongs to the top face (patch X by Z); its front-side vertices sit on that patch's edge.
        int edge = Enumerable.Range(0, mesh.VertexCount)
            .First(i => MathF.Abs(mesh.Positions[i].Y - 0.9f) < Epsilon && MathF.Abs(mesh.Positions[i].Z - 0.5f) < Epsilon
                && mesh.PatchSizes[i] == new Vector2(size.X, size.Z));
        Vector2 uv = mesh.Uvs[edge], patch = mesh.PatchSizes[edge];
        float toEdge = MathF.Min(MathF.Min(uv.X, patch.X - uv.X), MathF.Min(uv.Y, patch.Y - uv.Y));
        Assert.True(toEdge < Epsilon);
    }

    [Fact]
    public void HeightsStartAtTheLowestPoint()
    {
        var b = new MeshBuilder();
        b.Box(new Vector3(0f, 5f, 0f), new Vector3(1f, 2f, 1f), 0.05f);
        MeshData mesh = b.Finish();

        Assert.Equal(0f, mesh.Heights.Min(), 4);
        Assert.Equal(2f, mesh.Heights.Max(), 4);
    }

    [Fact]
    public void RectProfileIsClosedWithOutwardNormals()
    {
        Profile p = Profile.Rect(2f, 1f, 0.1f);

        Assert.Equal(8, p.Segments.Count);
        for (int i = 0; i < p.Segments.Count; i++)
        {
            Profile.Segment s = p.Segments[i];
            AssertClose(s.B, p.Segments[(i + 1) % p.Segments.Count].A);
            Assert.True(Vector2.Dot(s.NormalA, s.A) > 0f);
            Assert.True(Vector2.Dot(s.NormalB, s.B) > 0f);
        }
    }

    [Fact]
    public void PolygonProfileAcceptsEitherWinding()
    {
        Vector2[] square = { new(-1f, -1f), new(1f, -1f), new(1f, 1f), new(-1f, 1f) };
        foreach (Profile p in new[] { Profile.Polygon(square), Profile.Polygon(square.Reverse().ToArray()) })
        {
            foreach (Profile.Segment s in p.Segments)
            {
                Assert.True(Vector2.Dot(s.NormalA, (s.A + s.B) * 0.5f) > 0f, "side normals point outward");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void CappedSweepIsClosedAndFacesOut(string name)
    {
        Profile profile = ProfileNamed(name);
        var b = new MeshBuilder();
        List<PathFrame> path = SweepPath.Polyline(
            new[] { Vector3.Zero, new Vector3(0f, 3f, 0f), new Vector3(2f, 3f, 1f) }, Vector3.UnitZ, 0.5f);
        b.Sweep(path, profile);
        MeshData mesh = b.Finish();

        AssertWatertight(mesh);
        AssertFrontFacesFollowNormals(mesh);
        AssertFramesValid(mesh);
    }

    public static IEnumerable<object[]> Profiles() => new[] { "circle", "octagon", "rect", "gear" }.Select(n => new object[] { n });

    [Fact]
    public void RoundPipeGirthWrapsAround()
    {
        var b = new MeshBuilder();
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(5f, 0f, 0f), Vector3.UnitY), Profile.Circle(0.2f, 16), false, false);
        MeshData mesh = b.Finish();

        Assert.All(mesh.PatchSizes, size =>
        {
            Assert.Equal(5f, size.X, 4);
            Assert.Equal(-MathF.Tau * 0.2f, size.Y, 4);
        });
    }

    [Theory]
    [InlineData(6f, 3f)] // semicircle
    [InlineData(6f, 2f)] // segmental
    [InlineData(6f, 5.2f)] // pointed
    public void ArchSpringsFromBothSidesAndReachesItsRise(float span, float rise)
    {
        List<List<PathFrame>> halves = SweepPath.Arch(span, rise, 16);

        AssertClose(new Vector3(-span / 2f, 0f, 0f), halves[0][0].Position);
        AssertClose(new Vector3(span / 2f, 0f, 0f), halves[^1][^1].Position);
        float top = halves.SelectMany(h => h).Max(f => f.Position.Y);
        Assert.Equal(rise, top, 3);
        foreach (PathFrame f in halves.SelectMany(h => h))
        {
            Assert.Equal(1f, f.Side.Length(), 4);
            Assert.True(Vector3.Dot(f.Side, f.Position - new Vector3(0f, -10f, 0f)) > 0f, "side axis points out of the arch");
            Assert.Equal(Vector3.UnitZ, f.Up);
        }
    }

    [Fact]
    public void PointedArchHalvesMeetOnTheMitre()
    {
        List<List<PathFrame>> halves = SweepPath.Arch(6f, 5.2f, 16);
        Assert.Equal(2, halves.Count);

        Profile ring = Profile.Rect(0.6f, 1f, 0.05f);
        foreach (Vector2 p in ring.Outline())
        {
            Vector3 left = halves[0][^1].Place(p), right = halves[1][0].Place(p);
            AssertClose(left, right);
            Assert.Equal(0f, left.X, 4);
        }

        Assert.Equal(halves[0][^1].Distance, halves[1][0].Distance, 4);
    }

    [Fact]
    public void PolylineBendsKeepTheEndsAndTurnSmoothly()
    {
        Vector3[] points = { Vector3.Zero, new(4f, 0f, 0f), new(4f, 4f, 0f), new(8f, 4f, 0f) };
        List<PathFrame> path = SweepPath.Polyline(points, Vector3.UnitZ, 0.8f);

        AssertClose(points[0], path[0].Position);
        AssertClose(points[^1], path[^1].Position);
        for (int i = 1; i < path.Count; i++)
        {
            Assert.True(Vector3.Dot(path[i - 1].Tangent, path[i].Tangent) > MathF.Cos(0.35f), "no sharp turn between stations");
            Assert.True(path[i].Distance > path[i - 1].Distance);
        }

        Assert.True(path.All(f => MathF.Abs(Vector3.Dot(f.Up, f.Tangent)) < Epsilon));
    }

    [Fact]
    public void HangingCableDipsBySagAtItsMiddle()
    {
        List<PathFrame> path = SweepPath.Hanging(Vector3.Zero, new Vector3(6f, 0f, 0f), 1.5f, 24);

        Assert.Equal(-1.5f, path.Min(f => f.Position.Y), 4);
        AssertClose(new Vector3(6f, 0f, 0f), path[^1].Position);
    }

    [Fact]
    public void SliceIsCenteredOnTheRequestedStretch()
    {
        List<PathFrame> path = SweepPath.Line(Vector3.Zero, new Vector3(10f, 0f, 0f), Vector3.UnitY);
        List<PathFrame> slice = SweepPath.Slice(path, 3f, 3.2f);

        AssertClose(new Vector3(3f, 0f, 0f), slice[0].Position);
        AssertClose(new Vector3(3.2f, 0f, 0f), slice[1].Position);
    }

    [Theory]
    [MemberData(nameof(Pieces))]
    public void EveryKitPieceIsWellFormed(string name)
    {
        MeshData mesh = BuildPiece(name, seed: 3);

        Assert.True(mesh.TriangleCount > 10);
        AssertFrontFacesFollowNormals(mesh);
        AssertFramesValid(mesh);
        Assert.Equal(0f, mesh.Heights.Min(), 4);
        Assert.All(mesh.Variations, v => Assert.InRange(v, 0f, 1f));
        Assert.Equal(mesh.VertexCount, mesh.Cavities.Count);
        Assert.All(mesh.Cavities, c => Assert.InRange(c, 0f, 1f));
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.VertexCount - 1));
    }

    public static IEnumerable<object[]> Pieces() =>
        new[] { "column", "cluster", "pipe", "cables", "arch", "rib", "grate", "rack", "wreck", "icicles", "organ", "curtain",
            "key", "crystal", "boss", "keyhole", "seal", "cog", "lever", "handle", "cradle" }.Select(n => new object[] { n });

    [Fact]
    public void SeededPiecesAreRepeatableAndVary()
    {
        MeshData a = BuildPiece("wreck", seed: 7), again = BuildPiece("wreck", seed: 7), other = BuildPiece("wreck", seed: 8);

        Assert.Equal(a.Positions, again.Positions);
        Assert.NotEqual(a.Positions, other.Positions);
    }

    [Fact]
    public void StandingPiecesRestOnTheFloor()
    {
        foreach (string name in new[] { "column", "cluster", "grate", "rack", "arch" })
        {
            (Vector3 min, _) = BuildPiece(name, seed: 1).Bounds();
            Assert.True(MathF.Abs(min.Y) < 0.05f, $"{name} bottom is at {min.Y}");
        }
    }

    [Fact]
    public void IciclesHangBelowTheirRootAndVaryBySeed()
    {
        MeshData a = BuildPiece("icicles", seed: 3), other = BuildPiece("icicles", seed: 4);
        (Vector3 min, Vector3 max) = a.Bounds();
        Assert.True(max.Y < 0.05f, $"icicles rise above their root to {max.Y}");
        Assert.True(min.Y < -0.2f && min.Y >= -1.5f - 0.01f, $"the longest reaches {min.Y}");
        Assert.NotEqual(a.Positions[10], other.Positions[10]);
    }

    [Fact]
    public void OrganPipesStandInARankWithMouthsCutIntoTheirFronts()
    {
        MeshData mesh = BuildPiece("organ", seed: 1);
        (Vector3 min, Vector3 max) = mesh.Bounds();

        Assert.True(MathF.Abs(min.Y) < 1e-3f, $"the toes are at {min.Y}");
        Assert.Equal(6f, max.Y, 3);
        // The tallest is 0.3 wide, the others narrower by the cube root of their height.
        Assert.Equal(1.6f + 0.3f * (MathF.Cbrt(4f / 6f) + MathF.Cbrt(5f / 6f)), max.X - min.X, 3);
        // The middle pipe's mouth, low on the pipe: an inner face sunk well inside its girth, facing the front and down, dark.
        Assert.Contains(Enumerable.Range(0, mesh.VertexCount), i =>
            mesh.Cavities[i] == 1f && mesh.Normals[i].Z > 0.3f && mesh.Normals[i].Y < -0.5f && MathF.Abs(mesh.Positions[i].X) < 0.25f
            && mesh.Positions[i].Z < 0.3f * 0.3f && mesh.Positions[i].Y is > 1f and < 2f);
    }

    [Fact]
    public void IceCurtainHangsFromItsRootToItsLengthInFrontOfItsBack()
    {
        MeshData a = BuildPiece("curtain", seed: 3), other = BuildPiece("curtain", seed: 4);
        (Vector3 min, Vector3 max) = a.Bounds();

        Assert.True(max.Y < 1e-3f, $"the curtain rises above its root to {max.Y}");
        Assert.Equal(-6f, min.Y, 3);
        Assert.True(min.Z > -1e-3f && max.Z <= 0.8f * 1.2f && max.Z > 0.4f, $"the curtain bulges {min.Z}..{max.Z}");
        Assert.True(min.X >= -2f - 1e-3f && max.X <= 2f + 1e-3f);
        Assert.NotEqual(a.Positions[40], other.Positions[40]);
    }

    [Fact]
    public void KeyStandsItsLengthWithItsBitOnTheRight()
    {
        (Vector3 min, Vector3 max) = BuildPiece("key", seed: 0).Bounds();

        Assert.Equal(-0.3f, min.Y, 2);
        Assert.Equal(0.3f, max.Y, 2);
        Assert.True(max.X > 0.13f * 0.9f && min.X > -0.14f, $"the bit reaches {max.X}, the bow {min.X}");
        Assert.True(max.Z - min.Z < 0.1f, "a key is flat toward the camera");
    }

    [Fact]
    public void CrystalIsClosedFacetedAndSpansItsPoints()
    {
        MeshData mesh = BuildPiece("crystal", seed: 0);

        AssertWatertight(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(0.05f + 0.3f, max.Y, 3);
        Assert.Equal(-0.05f - 0.2f, min.Y, 3);
        // Flat facets: every vertex of a triangle has its triangle's normal.
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            Assert.Equal(mesh.Normals[mesh.Indices[i]], mesh.Normals[mesh.Indices[i + 1]]);
        }
    }

    [Theory]
    [InlineData("boss")]
    [InlineData("keyhole")]
    [InlineData("seal")]
    [InlineData("cog")]
    public void LockPartsStandOnTheirBackPlaneFacingTheCamera(string name)
    {
        (Vector3 min, Vector3 max) = BuildPiece(name, seed: 0).Bounds();

        Assert.True(MathF.Abs(min.Z) < 0.01f, $"{name} starts at {min.Z}");
        Assert.True(max.Z > 0.015f && max.Z < 0.15f, $"{name} stands {max.Z} proud");
    }

    [Fact]
    public void LeverHousingRestsOnTheFloorUnderItsQuadrant()
    {
        (Vector3 min, Vector3 max) = BuildPiece("lever", seed: 0).Bounds();

        Assert.True(MathF.Abs(min.Y) < 1e-3f);
        Assert.True(max.Y > 0.5f + 0.55f && max.Y < 0.5f + 0.55f + 0.12f, $"the quadrant tops out at {max.Y}");
        (Vector3 hMin, Vector3 hMax) = BuildPiece("handle", seed: 0).Bounds();
        Assert.Equal(0.9f, hMax.Y, 3);
        Assert.True(hMin.Y < 0f, "the hub sits across the pivot");
    }

    [Fact]
    public void CradleRisesFromItsCollarToItsHeight()
    {
        (Vector3 min, Vector3 max) = BuildPiece("cradle", seed: 0).Bounds();

        Assert.True(MathF.Abs(min.Y) < 0.02f, $"the collar sits at {min.Y}");
        Assert.True(max.Y > 0.38f && max.Y < 0.45f, $"the prongs reach {max.Y}");
    }

    private static MeshData BuildPiece(string name, int seed)
    {
        var b = new MeshBuilder();
        switch (name)
        {
            case "column": KitShapes.Column(b, 8f, 0.5f, 8, 1, 0.6f, 0.5f); break;
            case "cluster": KitShapes.Column(b, 8f, 0.6f, 20, 4, 0.6f, 0.5f); break;
            case "pipe": KitShapes.Pipe(b, new[] { Vector3.Zero, new Vector3(6f, 0f, 0f), new Vector3(6f, -4f, 0f) }, 0.2f, 0.6f, 1.5f); break;
            case "cables": KitShapes.Cables(b, new Vector3(8f, 1f, 0f), 1.2f, 0.04f, 3, seed); break;
            case "arch": KitShapes.Arch(b, 6f, 3f, 0.6f, 1f, 0f, 24); break;
            case "rib": KitShapes.Arch(b, 10f, 8f, 0.35f, 0.4f, 0.3f, 32); break;
            case "grate": KitShapes.Grate(b, new Vector3(4f, 0.25f, 3f), 0.15f, 0.04f); break;
            case "rack": KitShapes.Rack(b, new Vector3(1f, 2.4f, 1f), 12, seed); break;
            case "wreck": KitShapes.Wreck(b, 3f, seed); break;
            case "icicles": KitShapes.Icicles(b, 3f, 1.5f, 9, seed); break;
            case "organ": OrganPipeShape.Rank(b, new[] { 4f, 6f, 5f }, 0.8f, 0.3f); break;
            case "curtain": IceCurtainShape.Build(b, 4f, 6f, 0.8f, seed); break;
            case "key": PropShapes.Key(b, 0.6f, 0.13f, 0.04f); break;
            case "crystal": PropShapes.Crystal(b, 0.15f, 6, 0.1f, 0.3f, 0.2f); break;
            case "boss": PropShapes.Boss(b, 0.25f, 0.08f, 8); break;
            case "keyhole": PropShapes.Keyhole(b, 0.2f, 0.02f); break;
            case "seal": PropShapes.Seal(b, 0.42f, 0.07f); break;
            case "cog": PropShapes.Cog(b, 0.25f, 12, 0.07f); break;
            case "lever": PropShapes.LeverBase(b, new Vector3(0.62f, 0.34f, 0.5f), 0.5f, 0.55f, MathF.PI / 4f); break;
            case "handle": PropShapes.LeverHandle(b, 0.9f, 0.035f); break;
            case "cradle": PropShapes.Cradle(b, 0.22f, 0.4f, 3); break;
            default: throw new ArgumentException(name);
        }

        return b.Finish();
    }

    private static Profile ProfileNamed(string name) => name switch
    {
        "circle" => Profile.Circle(0.3f, 16),
        "octagon" => Profile.Circle(0.3f, 8, smooth: false),
        "rect" => Profile.Rect(0.6f, 0.4f, 0.05f),
        "gear" => Profile.Gear(0.25f, 0.35f, 10),
        _ => throw new ArgumentException(name),
    };

    /// <summary>Every edge, matched by position, is shared by exactly two triangles running it in opposite directions.</summary>
    private static void AssertWatertight(MeshData mesh)
    {
        var edges = new Dictionary<(Key, Key), int>();
        for (int t = 0; t < mesh.Indices.Count; t += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                Key from = Key.Of(mesh.Positions[mesh.Indices[t + e]]);
                Key to = Key.Of(mesh.Positions[mesh.Indices[t + (e + 1) % 3]]);
                edges[(from, to)] = edges.GetValueOrDefault((from, to)) + 1;
            }
        }

        foreach (((Key from, Key to), int count) in edges)
        {
            Assert.Equal(1, count);
            Assert.True(edges.ContainsKey((to, from)), $"open edge {from} -> {to}");
        }
    }

    /// <summary>Godot's front faces are clockwise: their geometric normal must agree with the vertex normals.</summary>
    private static void AssertFrontFacesFollowNormals(MeshData mesh)
    {
        for (int t = 0; t < mesh.Indices.Count; t += 3)
        {
            int a = mesh.Indices[t], b = mesh.Indices[t + 1], c = mesh.Indices[t + 2];
            Vector3 clockwiseNormal = Vector3.Cross(mesh.Positions[c] - mesh.Positions[a], mesh.Positions[b] - mesh.Positions[a]);
            Vector3 normals = mesh.Normals[a] + mesh.Normals[b] + mesh.Normals[c];
            Assert.True(Vector3.Dot(clockwiseNormal, normals) > 0f, $"triangle {t / 3} faces away from its normals");
        }
    }

    private static void AssertFramesValid(MeshData mesh)
    {
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            Vector3 n = mesh.Normals[i];
            Vector4 t = mesh.Tangents[i];
            var tangent = new Vector3(t.X, t.Y, t.Z);
            Assert.False(float.IsNaN(n.X + n.Y + n.Z + t.X + t.Y + t.Z), $"vertex {i} has NaN");
            Assert.Equal(1f, n.Length(), 3);
            Assert.Equal(1f, tangent.Length(), 3);
            Assert.True(MathF.Abs(Vector3.Dot(n, tangent)) < 1e-3f, $"vertex {i}: tangent not perpendicular to normal");
            Assert.True(MathF.Abs(t.W) == 1f);
        }
    }

    private static void AssertClose(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-3f, $"expected {expected}, got {actual}");

    private static void AssertClose(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 1e-3f, $"expected {expected}, got {actual}");

    private readonly record struct Key(long X, long Y, long Z)
    {
        public static Key Of(Vector3 p) => new((long)MathF.Round(p.X * 1e4f), (long)MathF.Round(p.Y * 1e4f), (long)MathF.Round(p.Z * 1e4f));
    }
}
