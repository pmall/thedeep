using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The room a turning piece takes over a full turn, pure: the axis-aligned box around every place the points of a convex shape
/// (a box's eight corners) pass while it turns about an axis. Each point runs a circle round the axis and the shape stays inside the
/// hull of those circles, so the box of the circles holds everything the shape sweeps. The room check holds a spinner's swept box to
/// the play plane's rule, as it holds a still mesh's box.
/// </summary>
public static class SpinSweep
{
    /// <summary>
    /// The box around the circles <paramref name="points"/> run turning about the line through <paramref name="pivot"/> along
    /// <paramref name="axis"/> (any length but zero). With several turns nested, sweep about the innermost first, then sweep that
    /// box's corners (<see cref="Corners"/>) about the next one out.
    /// </summary>
    public static (Vector3 Min, Vector3 Max) Bounds(IEnumerable<Vector3> points, Vector3 pivot, Vector3 axis)
    {
        Vector3 a = Vector3.Normalize(axis);

        // A circle of radius r about a reaches r·sqrt(1 - a_i²) either side of its center along each world axis i.
        var reach = new Vector3(Reach(a.X), Reach(a.Y), Reach(a.Z));
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (Vector3 p in points)
        {
            Vector3 center = pivot + (Vector3.Dot(p - pivot, a) * a);
            float r = Vector3.Distance(p, center);
            min = Vector3.Min(min, center - (r * reach));
            max = Vector3.Max(max, center + (r * reach));
        }

        return (min, max);
    }

    /// <summary>The eight corners of the box from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static Vector3[] Corners(Vector3 min, Vector3 max)
    {
        var corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            corners[i] = new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
        }

        return corners;
    }

    private static float Reach(float component) => MathF.Sqrt(MathF.Max(0f, 1f - (component * component)));
}
