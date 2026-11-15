using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The kit's heaps and fringes as pure geometry (Godot-free, unit tested): broken machinery and hanging icicles, each different
/// for each seed. Same conventions as <see cref="KitShapes"/>.
/// </summary>
public static class DebrisShapes
{
    /// <summary>
    /// A heap of broken machinery about <paramref name="size"/> meters across, different for each
    /// <paramref name="seed"/>: a fallen tank, a gear, tilted plates and snapped pipe stubs.
    /// </summary>
    public static void Wreck(MeshBuilder b, float size, int seed)
    {
        var random = new Random(seed);
        float R(float min, float max) => min + (max - min) * (float)random.NextDouble();
        Matrix4x4 placement = b.Transform;

        // A cylindrical tank lying on its side, slightly sunk, along X with a little yaw.
        float tankRadius = size * R(0.16f, 0.22f);
        float tankLength = size * R(0.5f, 0.7f);
        float yaw = R(-0.5f, 0.5f);
        b.Transform = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(R(-0.15f, 0.15f) * size, tankRadius * 0.85f, 0f) * placement;
        b.Variation = R(0f, 1f);
        b.Sweep(SweepPath.Line(new Vector3(-tankLength * 0.5f, 0f, 0f), new Vector3(tankLength * 0.5f, 0f, 0f), Vector3.UnitY),
            Profile.Circle(tankRadius, 20));
        // Hoops around the tank.
        for (int i = -1; i <= 1; i += 2)
        {
            float x = i * tankLength * 0.3f;
            b.Sweep(SweepPath.Line(new Vector3(x - 0.04f, 0f, 0f), new Vector3(x + 0.04f, 0f, 0f), Vector3.UnitY),
                Profile.Circle(tankRadius * 1.07f, 20));
        }

        // A gear leaning against the heap.
        float gearRadius = size * R(0.18f, 0.26f);
        float gearSide = random.NextDouble() < 0.5 ? -1f : 1f;
        b.Transform = Matrix4x4.CreateRotationZ(gearSide * R(0.15f, 0.4f))
            * Matrix4x4.CreateTranslation(gearSide * size * R(0.25f, 0.38f), gearRadius * 0.95f, R(-0.1f, 0.25f) * size) * placement;
        b.Variation = R(0f, 1f);
        b.Sweep(SweepPath.Line(new Vector3(0f, 0f, -0.07f), new Vector3(0f, 0f, 0.07f), Vector3.UnitY),
            Profile.Gear(gearRadius * 0.82f, gearRadius, 12));
        b.Sweep(SweepPath.Line(new Vector3(0f, 0f, -0.14f), new Vector3(0f, 0f, 0.14f), Vector3.UnitY),
            Profile.Circle(gearRadius * 0.2f, 12));

        // Plates fallen against the tank.
        for (int i = 0; i < 3; i++)
        {
            float w = size * R(0.2f, 0.4f), h = size * R(0.15f, 0.3f);
            b.Transform = Matrix4x4.CreateRotationX(R(-0.6f, 0.6f)) * Matrix4x4.CreateRotationZ(R(-0.9f, 0.9f))
                * Matrix4x4.CreateRotationY(R(-0.8f, 0.8f))
                * Matrix4x4.CreateTranslation(R(-0.4f, 0.4f) * size, h * 0.3f, R(-0.3f, 0.3f) * size) * placement;
            b.Variation = R(0f, 1f);
            b.Box(Vector3.Zero, new Vector3(w, h, 0.05f), 0.015f);
        }

        // Snapped pipe stubs sticking out.
        for (int i = 0; i < 2; i++)
        {
            float radius = size * R(0.03f, 0.06f);
            b.Transform = Matrix4x4.CreateRotationZ(R(-1.2f, 1.2f)) * Matrix4x4.CreateRotationY(R(0f, MathF.Tau))
                * Matrix4x4.CreateTranslation(R(-0.3f, 0.3f) * size, 0f, R(-0.2f, 0.2f) * size) * placement;
            b.Variation = R(0f, 1f);
            b.Sweep(SweepPath.Line(Vector3.Zero, new Vector3(0f, size * R(0.3f, 0.6f), 0f), Vector3.UnitZ),
                Profile.Circle(radius, 10));
        }

        b.Transform = placement;
    }

    /// <summary>
    /// A fringe of icicles hanging from y = 0 along X over <paramref name="width"/> meters (centered), each a tapered
    /// spike ending in a point, the longest <paramref name="length"/>; <paramref name="count"/> of them, a little in
    /// depth too, different for each <paramref name="seed"/>. Rime under a ledge or a beam. <paramref name="spread"/> 1 makes
    /// most of them short with now and then a long one; 0 makes them all full length (the columns of a frozen fall).
    /// </summary>
    public static void Icicles(MeshBuilder b, float width, float length, int count, int seed, float spread = 1f)
    {
        var random = new Random(seed);
        float R(float min, float max) => min + (max - min) * (float)random.NextDouble();
        for (int i = 0; i < count; i++)
        {
            float x = (count == 1 ? 0f : (i / (float)(count - 1) - 0.5f) * width) + R(-0.4f, 0.4f) * width / MathF.Max(count, 1);
            // Mostly short ones, now and then a long one.
            float t = (float)random.NextDouble();
            float len = length * (1f - spread * 0.85f * (1f - t * t * t));
            float radius = MathF.Max(0.02f, len * R(0.06f, 0.1f));
            var points = new List<Vector3>();
            var radii = new List<Vector2>();
            const int stations = 6;
            for (int k = 0; k < stations; k++)
            {
                float f = k / (float)(stations - 1);
                // A slight bulge near the root, then a long taper to a point; a little sway along it.
                points.Add(new Vector3(x + MathF.Sin(f * 2.1f + i) * radius * 0.4f, -len * f, R(-0.15f, 0.15f) * radius));
                float r = radius * (f < 0.15f ? 1f + f * 2f : 1.3f * MathF.Pow(1f - (f - 0.15f) / 0.85f, 1.4f));
                radii.Add(new Vector2(r, r));
            }

            b.Variation = R(0f, 1f);
            b.Pod(SweepPath.FromPoints(points, Vector3.UnitZ), radii, 7);
        }
    }
}
