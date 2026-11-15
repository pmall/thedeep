using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A door's lock as pure geometry (Godot-free, unit tested): a key's boss and keyhole, a code's seal, a mechanism's cog. Each
/// faces the camera (+Z) with its back on the origin's plane; the kit's LockMesh, KeyholeMesh, SealMesh and CogMesh expose them to scenes.
/// </summary>
public static class LockShapes
{
    /// <summary>
    /// A lock's boss facing the camera, its back on the origin's plane: a round housing <paramref name="radius"/> wide and
    /// <paramref name="depth"/> deep, a raised rim round its face, and <paramref name="studs"/> studs between rim and middle.
    /// </summary>
    public static void Boss(MeshBuilder b, float radius, float depth, int studs)
    {
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Circle(radius * 0.9f, 28), capStart: false);
        b.Variation = 0.3f;
        Matrix4x4 placement = b.Transform;
        b.Transform = Matrix4x4.CreateTranslation(0f, 0f, depth * 0.6f) * placement;
        b.Sweep(SweepPath.Arc(Vector2.Zero, radius * 0.93f, 0f, MathF.Tau, 32), Profile.Rect(radius * 0.14f, depth * 1.2f, radius * 0.04f),
            capStart: false, capEnd: false, wrapsAlong: true);
        b.Transform = placement;
        float stud = radius * 0.07f;
        for (int i = 0; i < studs; i++)
        {
            float a = MathF.Tau * (i + 0.5f) / studs;
            var at = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * (radius * 0.72f);
            b.Variation = 0.5f + 0.4f * i / Math.Max(1, studs);
            b.Sweep(SweepPath.Line(at + new Vector3(0f, 0f, depth * 0.9f), at + new Vector3(0f, 0f, depth + stud), Vector3.UnitY),
                Profile.Circle(stud, 8), capStart: false);
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// A keyhole <paramref name="height"/> tall facing the camera, <paramref name="depth"/> thick, its back on the origin's plane: a
    /// round eye over a slot that flares toward its foot. The eye's middle is at the origin.
    /// </summary>
    public static void Keyhole(MeshBuilder b, float height, float depth)
    {
        float r = height * 0.3f;
        float foot = -(height - r);
        var points = new List<Vector2>();
        const int Arc = 18;
        // From the slot's right shoulder, round the eye counterclockwise to its left shoulder, then down the slot.
        float from = -MathF.PI * 0.32f, to = MathF.PI * 1.32f;
        for (int i = 0; i <= Arc; i++)
        {
            float a = from + (to - from) * i / Arc;
            points.Add(new Vector2(MathF.Cos(a), MathF.Sin(a)) * r);
        }

        points.Add(new Vector2(-r * 0.62f, foot));
        points.Add(new Vector2(r * 0.62f, foot));
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Polygon(points));
    }

    /// <summary>
    /// A cog facing the camera, its back on the origin's plane: <paramref name="teeth"/> teeth out to <paramref name="radius"/>,
    /// <paramref name="depth"/> deep, a hub standing proud of it and spokes' windows between.
    /// </summary>
    public static void Cog(MeshBuilder b, float radius, int teeth, float depth)
    {
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Gear(radius * 0.84f, radius, teeth));
        b.Variation = 0.5f;
        b.Sweep(SweepPath.Line(new Vector3(0f, 0f, depth * 0.5f), new Vector3(0f, 0f, depth * 1.35f), Vector3.UnitY),
            Profile.Circle(radius * 0.36f, 18), capStart: false);
        b.Variation = 0.8f;
        Matrix4x4 placement = b.Transform;
        b.Transform = Matrix4x4.CreateTranslation(0f, 0f, depth * 0.58f) * placement;
        b.Sweep(SweepPath.Arc(Vector2.Zero, radius * 0.62f, 0f, MathF.Tau, 28), Profile.Rect(radius * 0.08f, depth * 1.15f, radius * 0.02f),
            capStart: false, capEnd: false, wrapsAlong: true);
        b.Transform = placement;
        b.Variation = 0f;
    }

    /// <summary>
    /// A code's seal facing the camera, its back on the origin's plane: a square plate <paramref name="size"/> wide with its corners
    /// cut, <paramref name="depth"/> deep, a raised frame round its face and a stud at each cut corner.
    /// </summary>
    public static void Seal(MeshBuilder b, float size, float depth)
    {
        float h = size * 0.5f, cut = size * 0.18f;
        var outline = new[]
        {
            new Vector2(-h + cut, -h), new Vector2(h - cut, -h), new Vector2(h, -h + cut), new Vector2(h, h - cut),
            new Vector2(h - cut, h), new Vector2(-h + cut, h), new Vector2(-h, h - cut), new Vector2(-h, -h + cut),
        };
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, 0f, depth), Vector3.UnitY), Profile.Polygon(outline), capStart: false);
        float bar = size * 0.08f;
        b.Variation = 0.3f;
        foreach (float s in new[] { -1f, 1f })
        {
            b.Box(new Vector3(0f, s * (h - bar * 0.5f - cut * 0.2f), depth + bar * 0.3f), new Vector3(size - 2f * cut, bar, bar * 0.9f), bar * 0.2f);
            b.Box(new Vector3(s * (h - bar * 0.5f - cut * 0.2f), 0f, depth + bar * 0.3f), new Vector3(bar, size - 2f * cut, bar * 0.9f), bar * 0.2f);
        }

        b.Variation = 0.6f;
        for (int i = 0; i < 4; i++)
        {
            float a = MathF.PI * 0.25f + MathF.PI * 0.5f * i;
            var at = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f) * ((h - cut * 0.4f) * MathF.Sqrt(2f) * 0.86f);
            b.Sweep(SweepPath.Line(at + new Vector3(0f, 0f, depth * 0.5f), at + new Vector3(0f, 0f, depth + bar * 0.6f), Vector3.UnitY),
                Profile.Circle(bar * 0.45f, 8), capStart: false);
        }

        b.Variation = 0f;
    }
}
