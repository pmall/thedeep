using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Builds kit meshes from a few primitives (bevelled boxes, sweeps, caps), Godot-free. Every vertex gets
/// the surface frame of <see cref="MeshData"/>. Triangles are oriented from the vertex normals, so
/// primitives only need outward normals, not a careful winding.
/// </summary>
public sealed class MeshBuilder
{
    private const float HeightUnset = float.NaN;

    private readonly MeshData _mesh = new();

    /// <summary>Placement applied to everything added next: rotation and translation only (UVs are meters).</summary>
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    /// <summary>Per-part random value given to the vertices added next (see <see cref="MeshData.Variations"/>).</summary>
    public float Variation { get; set; }

    /// <summary>
    /// Adds a vertex. <paramref name="uAxis"/> and <paramref name="vAxis"/> are the directions in which
    /// <paramref name="uv"/> grows; they are fitted to the normal.
    /// </summary>
    public int Vertex(Vector3 position, Vector3 normal, Vector3 uAxis, Vector3 vAxis, Vector2 uv, Vector2 patchSize)
    {
        Vector3 n = Vector3.Normalize(Vector3.TransformNormal(normal, Transform));
        Vector3 u = Vector3.TransformNormal(uAxis, Transform);
        Vector3 v = Vector3.TransformNormal(vAxis, Transform);
        Vector3 t = u - n * Vector3.Dot(n, u);
        if (t.LengthSquared() < 1e-8f)
        {
            // U runs along the normal (a corner vertex): pick the tangent that makes +V come out right.
            t = Vector3.Cross(v, n);
        }

        t = Vector3.Normalize(t);
        float w = Vector3.Dot(Vector3.Cross(n, t), v) >= 0f ? 1f : -1f;

        _mesh.Positions.Add(Vector3.Transform(position, Transform));
        _mesh.Normals.Add(n);
        _mesh.Tangents.Add(new Vector4(t, w));
        _mesh.Uvs.Add(uv);
        _mesh.PatchSizes.Add(patchSize);
        _mesh.Heights.Add(HeightUnset);
        _mesh.Variations.Add(Variation);
        return _mesh.Positions.Count - 1;
    }

    /// <summary>Adds a triangle, facing the side its vertex normals point to. Degenerate triangles are dropped.</summary>
    public void Triangle(int a, int b, int c)
    {
        List<Vector3> p = _mesh.Positions;
        Vector3 geometric = Vector3.Cross(p[b] - p[a], p[c] - p[a]);
        if (geometric.LengthSquared() < 1e-14f)
        {
            return;
        }

        Vector3 normal = _mesh.Normals[a] + _mesh.Normals[b] + _mesh.Normals[c];
        if (Vector3.Dot(geometric, normal) < 0f)
        {
            (b, c) = (c, b);
        }

        // a, b, c now run counterclockwise seen from the front; Godot's front faces are clockwise.
        _mesh.Indices.Add(a);
        _mesh.Indices.Add(c);
        _mesh.Indices.Add(b);
    }

    /// <summary>Adds a quad given as a loop (either direction).</summary>
    public void Quad(int a, int b, int c, int d)
    {
        Triangle(a, b, c);
        Triangle(a, c, d);
    }

    /// <summary>
    /// A box centered on <paramref name="center"/> with its edges cut by a chamfer of <paramref name="bevel"/>
    /// meters. Chamfer normals turn from one face to the next, so edges read as rounded and catch the light.
    /// Each face is a patch measured from the box's lower corner, as large as the whole face: the chamfer
    /// belongs to one of its faces and runs to that patch's edge.
    /// </summary>
    public void Box(Vector3 center, Vector3 size, float bevel)
    {
        Vector3 h = size * 0.5f;
        float r = Math.Clamp(bevel, 0f, 0.45f * MathF.Min(h.X, MathF.Min(h.Y, h.Z)));

        // Faces.
        for (int a = 0; a < 3; a++)
        {
            (int u, int v) = FaceAxes(a);
            foreach (float s in Signs)
            {
                var corners = new int[4];
                int k = 0;
                foreach ((float cu, float cv) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    Vector3 pos = Axis(a) * (s * Get(h, a)) + Axis(u) * (cu * (Get(h, u) - r)) + Axis(v) * (cv * (Get(h, v) - r));
                    corners[k++] = BoxVertex(center, pos, Axis(a) * s, a, h);
                }

                Quad(corners[0], corners[1], corners[2], corners[3]);
            }
        }

        if (r <= 0f)
        {
            return;
        }

        // Chamfers along the 12 edges, each owned by the face of its first axis.
        for (int a = 0; a < 3; a++)
        {
            for (int b = a + 1; b < 3; b++)
            {
                int c = 3 - a - b;
                foreach (float sa in Signs)
                {
                    foreach (float sb in Signs)
                    {
                        var ends = new int[4];
                        int k = 0;
                        foreach (float sc in Signs)
                        {
                            Vector3 along = Axis(c) * (sc * (Get(h, c) - r));
                            Vector3 onA = Axis(a) * (sa * Get(h, a)) + Axis(b) * (sb * (Get(h, b) - r)) + along;
                            Vector3 onB = Axis(a) * (sa * (Get(h, a) - r)) + Axis(b) * (sb * Get(h, b)) + along;
                            ends[k++] = BoxVertex(center, onA, Axis(a) * sa, a, h);
                            ends[k++] = BoxVertex(center, onB, Axis(b) * sb, a, h);
                        }

                        Quad(ends[0], ends[2], ends[3], ends[1]);
                    }
                }
            }
        }

        // Corner triangles, in the frame of the X face.
        foreach (float sx in Signs)
        {
            foreach (float sy in Signs)
            {
                foreach (float sz in Signs)
                {
                    var inner = new Vector3(sx * (h.X - r), sy * (h.Y - r), sz * (h.Z - r));
                    int ix = BoxVertex(center, inner with { X = sx * h.X }, Vector3.UnitX * sx, 0, h);
                    int iy = BoxVertex(center, inner with { Y = sy * h.Y }, Vector3.UnitY * sy, 0, h);
                    int iz = BoxVertex(center, inner with { Z = sz * h.Z }, Vector3.UnitZ * sz, 0, h);
                    Triangle(ix, iy, iz);
                }
            }
        }
    }

    /// <summary>
    /// Sweeps <paramref name="profile"/> along <paramref name="path"/>. U runs along the path (from its
    /// first station), V across each profile side. Ends are closed with flat caps when asked.
    /// <paramref name="scales"/>, one per station, stretches the profile there along its X and Y (a tapering
    /// blade or coat tail); normals follow the stretch, not the taper along the path. <paramref name="wrapsAlong"/>
    /// marks a path that closes on itself (a ring): its patches have no edge where it starts and ends.
    /// </summary>
    public void Sweep(IReadOnlyList<PathFrame> path, Profile profile, bool capStart = true, bool capEnd = true,
        IReadOnlyList<Vector2>? scales = null, bool wrapsAlong = false)
    {
        float start = path[0].Distance;
        float length = path[^1].Distance - start;
        foreach (Profile.Segment s in profile.Segments)
        {
            int previousA = -1, previousB = -1;
            Vector2 run = s.B - s.A;
            float runLength = run.Length();
            Vector2 runDirection = runLength > 1e-6f ? run / runLength : Vector2.UnitX;
            for (int i = 0; i < path.Count; i++)
            {
                PathFrame f = path[i];
                Vector2 k = scales?[i] ?? Vector2.One;
                // V is measured along the side, so it stretches with the profile axis the side runs along.
                float vScale = (runDirection * k).Length();
                Vector3 pa = f.Place(s.A * k), pb = f.Place(s.B * k);
                Vector3 across = pb - pa;
                var size = new Vector2(wrapsAlong ? -length : length, s.PatchWidth * vScale);
                float u = f.Distance - start;
                int ia = Vertex(pa, f.Direction(ScaledNormal(s.NormalA, k)), f.Tangent, across, new Vector2(u, s.VA * vScale), size);
                int ib = Vertex(pb, f.Direction(ScaledNormal(s.NormalB, k)), f.Tangent, across, new Vector2(u, s.VB * vScale), size);
                if (previousA >= 0)
                {
                    Quad(previousA, previousB, ib, ia);
                }

                previousA = ia;
                previousB = ib;
            }
        }

        if (capStart)
        {
            Cap(path[0], profile, -1f, scales?[0] ?? Vector2.One);
        }

        if (capEnd)
        {
            Cap(path[^1], profile, 1f, scales?[^1] ?? Vector2.One);
        }
    }

    /// <summary>
    /// A smooth closed body around <paramref name="path"/> (limbs, heads, shells, bulbs): at each station an
    /// ellipse with radii <paramref name="radii"/> (X along the frame's side axis, Y along its up axis),
    /// <paramref name="sides"/> points around. A station with zero radii is a pole, which rounds the end off;
    /// an end with a radius is closed by a flat cap. Normals come from the surface itself, so tapers, bulges
    /// and poles shade smoothly. U runs along the path, V around the girth (a patch that wraps).
    /// </summary>
    public void Pod(IReadOnlyList<PathFrame> path, IReadOnlyList<Vector2> radii, int sides)
    {
        int rings = path.Count;
        sides = Math.Max(3, sides);
        int columns = sides + 1; // the seam column repeats the first, so V can run to the full girth
        var grid = new Vector3[rings, columns];
        var girthAxes = new Vector3[rings, columns];
        float maxGirth = 0f;
        for (int j = 0; j < rings; j++)
        {
            PathFrame f = path[j];
            Vector2 r = radii[j];
            maxGirth = MathF.Max(maxGirth, Girth(r));
            for (int k = 0; k < columns; k++)
            {
                float a = MathF.Tau * k / sides;
                float cos = MathF.Cos(a), sin = MathF.Sin(a);
                grid[j, k] = f.Position + f.Side * (cos * r.X) + f.Up * (sin * r.Y);
                // Direction of growing V: the ellipse's tangent (a circle's at a pole).
                Vector3 along = f.Side * (-sin * MathF.Max(r.X, 1e-3f)) + f.Up * (cos * MathF.Max(r.Y, 1e-3f));
                girthAxes[j, k] = Vector3.Normalize(along);
            }
        }

        float start = path[0].Distance;
        var size = new Vector2(path[^1].Distance - start, -MathF.Max(maxGirth, 1e-3f));
        var index = new int[rings, columns];
        for (int j = 0; j < rings; j++)
        {
            PathFrame f = path[j];
            float girth = Girth(radii[j]);
            for (int k = 0; k < columns; k++)
            {
                Vector3 alongPath = grid[Math.Min(j + 1, rings - 1), k] - grid[Math.Max(j - 1, 0), k];
                Vector3 alongGirth = grid[j, (k + 1) % sides] - grid[j, (k + sides - 1) % sides];
                Vector3 normal = Vector3.Cross(alongGirth, alongPath);
                Vector3 outward = grid[j, k] - f.Position;
                if (normal.LengthSquared() < 1e-12f || outward.LengthSquared() < 1e-10f)
                {
                    // A pole: the surface faces along the path.
                    normal = j == 0 ? -f.Tangent : j == rings - 1 ? f.Tangent : outward;
                }
                else if (Vector3.Dot(normal, outward) < 0f)
                {
                    normal = -normal;
                }

                var uv = new Vector2(f.Distance - start, girth * k / sides);
                index[j, k] = Vertex(grid[j, k], normal, f.Tangent, girthAxes[j, k], uv, size);
            }
        }

        for (int j = 0; j + 1 < rings; j++)
        {
            for (int k = 0; k < sides; k++)
            {
                Quad(index[j, k], index[j, k + 1], index[j + 1, k + 1], index[j + 1, k]);
            }
        }

        if (radii[0].LengthSquared() > 1e-10f)
        {
            Cap(path[0], Profile.Ellipse(radii[0], sides), -1f, Vector2.One);
        }

        if (radii[^1].LengthSquared() > 1e-10f)
        {
            Cap(path[^1], Profile.Ellipse(radii[^1], sides), 1f, Vector2.One);
        }
    }

    /// <summary>
    /// A flat cap closing <paramref name="profile"/> at <paramref name="frame"/>, facing along the path
    /// (<paramref name="facing"/> +1) or against it (-1). A fan from the outline's center, so the profile
    /// must be star-shaped around it (circles, rectangles, gears are).
    /// </summary>
    public void Cap(PathFrame frame, Profile profile, float facing) => Cap(frame, profile, facing, Vector2.One);

    private void Cap(PathFrame frame, Profile profile, float facing, Vector2 scale)
    {
        List<Vector2> outline = profile.Outline().Select(p => p * scale).ToList();
        Vector2 min = outline[0], max = outline[0];
        Vector2 sum = Vector2.Zero;
        foreach (Vector2 p in outline)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
            sum += p;
        }

        Vector2 size = max - min;
        Vector3 normal = frame.Tangent * facing;
        int Add(Vector2 p) => Vertex(frame.Place(p), normal, frame.Side, frame.Up, p - min, size);

        int center = Add(sum / outline.Count);
        int first = Add(outline[0]);
        int previous = first;
        for (int i = 1; i < outline.Count; i++)
        {
            int current = Add(outline[i]);
            Triangle(center, previous, current);
            previous = current;
        }

        Triangle(center, previous, first);
    }

    /// <summary>Finishes the mesh: heights are measured from its lowest point.</summary>
    public MeshData Finish()
    {
        float bottom = _mesh.Positions.Count > 0 ? _mesh.Positions.Min(p => p.Y) : 0f;
        for (int i = 0; i < _mesh.Positions.Count; i++)
        {
            _mesh.Heights[i] = _mesh.Positions[i].Y - bottom;
        }

        return _mesh;
    }

    private static readonly float[] Signs = { -1f, 1f };

    /// <summary>A profile normal after the profile is stretched by <paramref name="scale"/> (normals scale inversely).</summary>
    private static Vector2 ScaledNormal(Vector2 normal, Vector2 scale)
    {
        var n = new Vector2(normal.X / MathF.Max(scale.X, 1e-4f), normal.Y / MathF.Max(scale.Y, 1e-4f));
        return n.LengthSquared() > 1e-12f ? Vector2.Normalize(n) : normal;
    }

    /// <summary>Approximate circumference of an ellipse with radii <paramref name="r"/> (Ramanujan).</summary>
    private static float Girth(Vector2 r)
    {
        float a = MathF.Abs(r.X), b = MathF.Abs(r.Y);
        return MathF.PI * (3f * (a + b) - MathF.Sqrt((3f * a + b) * (a + 3f * b)));
    }

    /// <summary>The axes a box face's patch runs along (U, V): X faces (Z, Y), Y faces (X, Z), Z faces (X, Y).</summary>
    private static (int U, int V) FaceAxes(int normalAxis) => normalAxis switch
    {
        0 => (2, 1),
        1 => (0, 2),
        _ => (0, 1),
    };

    private int BoxVertex(Vector3 center, Vector3 local, Vector3 normal, int frameAxis, Vector3 half)
    {
        (int u, int v) = FaceAxes(frameAxis);
        var uv = new Vector2(Get(local, u) + Get(half, u), Get(local, v) + Get(half, v));
        var size = new Vector2(2f * Get(half, u), 2f * Get(half, v));
        return Vertex(center + local, normal, Axis(u), Axis(v), uv, size);
    }

    private static Vector3 Axis(int i) => i switch
    {
        0 => Vector3.UnitX,
        1 => Vector3.UnitY,
        _ => Vector3.UnitZ,
    };

    private static float Get(Vector3 v, int i) => i switch
    {
        0 => v.X,
        1 => v.Y,
        _ => v.Z,
    };
}
