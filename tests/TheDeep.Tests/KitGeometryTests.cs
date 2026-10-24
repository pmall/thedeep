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
        Assert.All(mesh.Indices, i => Assert.InRange(i, 0, mesh.VertexCount - 1));
    }

    public static IEnumerable<object[]> Pieces() =>
        new[] { "column", "cluster", "pipe", "cables", "arch", "rib", "grate", "rack", "wreck" }.Select(n => new object[] { n });

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
