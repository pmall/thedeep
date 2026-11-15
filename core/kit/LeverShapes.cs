using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A lever as pure geometry (Godot-free, unit tested): its housing and its handle; the kit's LeverMesh and LeverHandleMesh expose them
/// to scenes. A piece seen face-on faces the camera (+Z); X is across the screen, Y up.
/// </summary>
public static class LeverShapes
{
    /// <summary>
    /// A lever's housing standing on the origin, its handle's pivot <paramref name="pivotHeight"/> above the floor: a stepped plinth
    /// (<paramref name="size"/>: width, height, depth) with ribs down its front, two cheeks holding the pivot's drum between them,
    /// and a quadrant of two rails arching over the pivot at <paramref name="guideRadius"/>, <paramref name="guideAngle"/> radians
    /// either side of upright, its ticks marking the throw, its ends stayed down to the plinth's lower step.
    /// </summary>
    public static void LeverBase(MeshBuilder b, Vector3 size, float pivotHeight, float guideRadius, float guideAngle)
    {
        float low = size.Y * 0.4f;
        b.Variation = 0f;
        b.Box(new Vector3(0f, low * 0.5f, 0f), new Vector3(size.X, low, size.Z), 0.03f);
        b.Variation = 0.15f;
        b.Box(new Vector3(0f, low + (size.Y - low) * 0.5f, 0f), new Vector3(size.X * 0.78f, size.Y - low, size.Z * 0.8f), 0.025f);

        // Ribs down the plinth's front.
        b.Variation = 0.35f;
        for (int i = -1; i <= 1; i += 2)
        {
            b.Box(new Vector3(i * size.X * 0.28f, size.Y * 0.5f, size.Z * 0.42f), new Vector3(size.X * 0.06f, size.Y * 0.9f, size.Z * 0.1f), 0.01f);
        }

        // The cheeks and the drum between them.
        float cheekZ = size.Z * 0.24f, cheek = size.Z * 0.08f;
        float cheekTop = pivotHeight + size.X * 0.12f;
        b.Variation = 0.5f;
        foreach (float s in new[] { -1f, 1f })
        {
            b.Box(new Vector3(0f, (size.Y + cheekTop) * 0.5f, s * cheekZ), new Vector3(size.X * 0.32f, cheekTop - size.Y, cheek), 0.015f);
        }

        b.Variation = 0.7f;
        b.Sweep(SweepPath.Line(new Vector3(0f, pivotHeight, -cheekZ - cheek), new Vector3(0f, pivotHeight, cheekZ + cheek), Vector3.UnitY),
            Profile.Circle(size.X * 0.1f, 18));

        // The quadrant: two rails over the pivot, ticks across them, stays down to the plinth.
        float from = MathF.PI * 0.5f - guideAngle, to = MathF.PI * 0.5f + guideAngle;
        var pivot = new Vector2(0f, pivotHeight);
        float rail = size.X * 0.05f;
        b.Variation = 0.25f;
        foreach (float s in new[] { -1f, 1f })
        {
            Matrix4x4 placement = b.Transform;
            b.Transform = Matrix4x4.CreateTranslation(0f, 0f, s * cheekZ) * placement;
            b.Sweep(SweepPath.Arc(pivot, guideRadius, from, to, 16), Profile.Rect(rail, rail * 0.8f, rail * 0.2f));
            b.Transform = placement;
        }

        b.Variation = 0.9f;
        const int Ticks = 5;
        for (int i = 0; i < Ticks; i++)
        {
            float a = from + (to - from) * i / (Ticks - 1);
            var radial = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
            Vector3 at = new Vector3(pivot, 0f) + radial * (guideRadius + rail * 1.2f);
            bool end = i == 0 || i == Ticks - 1;
            b.Sweep(SweepPath.Line(at - radial * rail * 1.4f, at + radial * rail * (end ? 1.6f : 0.9f), Vector3.UnitZ),
                Profile.Rect(rail * (end ? 1.1f : 0.6f), cheekZ * 2f + rail, rail * 0.15f));
        }

        b.Variation = 0.45f;
        foreach (float a in new[] { from, to })
        {
            var radial = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
            Vector3 end = new Vector3(pivot, 0f) + radial * guideRadius;
            var foot = new Vector3(MathF.Sign(end.X) * MathF.Min(MathF.Abs(end.X), size.X * 0.44f), low, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                var z = new Vector3(0f, 0f, s * cheekZ);
                b.Sweep(SweepPath.Line(foot + z, end + z, Vector3.UnitZ), Profile.Rect(rail * 0.8f, rail * 0.8f, rail * 0.2f));
            }
        }

        b.Variation = 0f;
    }

    /// <summary>
    /// A lever's handle rising from its pivot at the origin along +Y, <paramref name="length"/> long and <paramref name="radius"/>
    /// thick: a hub across the pivot, a tapering shaft with a collar, and the socket the grip sits across at its top.
    /// </summary>
    public static void LeverHandle(MeshBuilder b, float length, float radius)
    {
        b.Variation = 0f;
        b.Sweep(SweepPath.Line(new Vector3(0f, 0f, -radius * 1.6f), new Vector3(0f, 0f, radius * 1.6f), Vector3.UnitY),
            Profile.Circle(radius * 2.4f, 16));
        b.Variation = 0.3f;
        b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, length, 0f), Vector3.UnitZ), Profile.Circle(radius, 12),
            scales: new[] { Vector2.One, new Vector2(0.75f, 0.75f) });
        b.Variation = 0.6f;
        b.Sweep(SweepPath.Line(new Vector3(0f, length * 0.3f, 0f), new Vector3(0f, length * 0.3f + radius * 2f, 0f), Vector3.UnitZ),
            Profile.Circle(radius * 1.7f, 12));
        b.Sweep(SweepPath.Line(new Vector3(0f, length - radius * 2.2f, 0f), new Vector3(0f, length, 0f), Vector3.UnitZ),
            Profile.Circle(radius * 1.5f, 12));
        b.Variation = 0f;
    }
}
