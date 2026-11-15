using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>The bevelled box primitive of <see cref="MeshBuilder"/>.</summary>
public sealed partial class MeshBuilder
{
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

    private static readonly float[] Signs = { -1f, 1f };

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
