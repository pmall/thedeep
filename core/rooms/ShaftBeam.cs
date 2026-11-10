using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The visible beam of a room's light shaft (a spot light shining in from above), Godot-free: how far it falls before the room's
/// solids stop it (<see cref="Reach"/>), and the soft mesh drawn along it (<see cref="Build"/>), a flat strip through the light's
/// axis turned to face the camera, as wide as the cone and a halo around it. The camera looks down -Z and barely turns, so one
/// strip reads as a volume; the material draws the light's falloff on it (assets/shaders/light_shaft.gdshader).
/// </summary>
public static class ShaftBeam
{
    /// <summary>Rows along the beam: enough that the strip's width across, interpolated per triangle, stays true to the cone.</summary>
    public const int Rows = 16;

    /// <summary>The strip's half width at the light, meters: the beam starts as a narrow ray, not a point.</summary>
    private const float SourceHalfWidth = 0.25f;

    /// <summary>
    /// How far along its axis (meters) a beam from <paramref name="origin"/> along <paramref name="direction"/> falls before it meets
    /// one of <paramref name="solids"/> or sinks under <paramref name="floorY"/> (the bottom of the room's view), at most
    /// <paramref name="range"/>. A solid stops the beam from its back face toward the camera, as the room's dressing and dark
    /// foreground carry it on in front: a beam passing behind a solid goes on, one crossing in front of it lands there. A solid the
    /// light starts inside doesn't stop it.
    /// </summary>
    public static float Reach(Vector3 origin, Vector3 direction, float range, float floorY, IReadOnlyList<SolidBox> solids)
    {
        Vector3 axis = Vector3.Normalize(direction);
        float reach = axis.Y < 0f && origin.Y > floorY ? MathF.Min(range, (floorY - origin.Y) / axis.Y) : range;
        foreach (SolidBox solid in solids)
        {
            var box = new SolidBox(solid.Min, new Vector3(solid.Max.X, solid.Max.Y, float.PositiveInfinity));
            if (box.Contains(origin))
            {
                continue;
            }

            if (Enters(origin, axis, box) is float t && t < reach)
            {
                reach = t;
            }
        }

        return reach;
    }

    /// <summary>
    /// The beam's strip: from <paramref name="origin"/> along <paramref name="direction"/> for <paramref name="length"/> meters, its half
    /// width the cone's (<paramref name="halfAngle"/>, radians) times <paramref name="spread"/>, turned to face <paramref name="toCamera"/>,
    /// positions relative to <paramref name="origin"/>. The strip never lies deeper than <paramref name="deepest"/> (Z): a light shining
    /// in from far behind the room would pass behind its back wall and show in pieces, so its beam is drawn brought forward, in front of
    /// the wall, where it reads as one column falling on what it lights. UV: X across, -1 to 1 edge to edge; Y along, 0 at the light to
    /// 1 at the end. UV2: X the length in meters, Y the spread, so the material knows where the cone's edge falls.
    /// </summary>
    public static BeamStrip Build(Vector3 origin, Vector3 direction, float halfAngle, float length, float spread, Vector3 toCamera, float deepest)
    {
        Vector3 axis = Vector3.Normalize(direction);
        Vector3 across = Vector3.Cross(axis, toCamera);
        across = across.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(across);
        float slope = MathF.Tan(halfAngle) * spread;

        var strip = new BeamStrip(new Vector3[(Rows + 1) * 2], new Vector2[(Rows + 1) * 2], new Vector2(length, spread), new int[Rows * 6]);
        for (int row = 0; row <= Rows; row++)
        {
            float along = (float)row / Rows;
            float distance = along * length;
            Vector3 center = axis * distance;
            center.Z = MathF.Max(center.Z, deepest - origin.Z);
            Vector3 half = across * MathF.Max(SourceHalfWidth, distance * slope);
            strip.Positions[row * 2] = center - half;
            strip.Positions[row * 2 + 1] = center + half;
            strip.Uvs[row * 2] = new Vector2(-1f, along);
            strip.Uvs[row * 2 + 1] = new Vector2(1f, along);
        }

        for (int row = 0; row < Rows; row++)
        {
            int a = row * 2;
            int[] i = strip.Indices;
            (i[row * 6], i[row * 6 + 1], i[row * 6 + 2]) = (a, a + 2, a + 1);
            (i[row * 6 + 3], i[row * 6 + 4], i[row * 6 + 5]) = (a + 1, a + 2, a + 3);
        }

        return strip;
    }

    /// <summary>Where a ray enters the box (its distance along the ray), or null if it misses or the box is behind.</summary>
    private static float? Enters(Vector3 o, Vector3 d, SolidBox box)
    {
        (float nearX, float farX) = Slab(o.X, d.X, box.Min.X, box.Max.X);
        (float nearY, float farY) = Slab(o.Y, d.Y, box.Min.Y, box.Max.Y);
        (float nearZ, float farZ) = Slab(o.Z, d.Z, box.Min.Z, box.Max.Z);
        float near = MathF.Max(nearX, MathF.Max(nearY, nearZ)), far = MathF.Min(farX, MathF.Min(farY, farZ));
        return near <= far && near > 0f ? near : null;
    }

    private static (float Near, float Far) Slab(float o, float d, float min, float max)
    {
        if (MathF.Abs(d) < 1e-6f)
        {
            return o >= min && o <= max ? (float.NegativeInfinity, float.PositiveInfinity) : (float.PositiveInfinity, float.NegativeInfinity);
        }

        float a = (min - o) / d, b = (max - o) / d;
        return a < b ? (a, b) : (b, a);
    }
}
