using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The environment kit's pieces as pure geometry (Godot-free, unit tested). Each one writes into a
/// <see cref="MeshBuilder"/>; the KitMesh resources expose them to scenes. Every piece has its origin
/// at the middle of its footprint on the floor (Y = 0), so it snaps by its base. The shelving is <see cref="ShelfShapes"/>, the
/// heaps and fringes <see cref="DebrisShapes"/>.
/// </summary>
public static class KitShapes
{
    /// <summary>A bevelled block of <paramref name="size"/>, centered on the origin (gameplay blocks).</summary>
    public static void Block(MeshBuilder b, Vector3 size, float bevel)
    {
        b.Box(Vector3.Zero, size, bevel);
    }

    /// <summary>
    /// A column: a square plinth, a shaft (round, or faceted when <paramref name="sides"/> is 12 or fewer)
    /// and a square capital. <paramref name="shafts"/> above 1 makes a clustered pier: thinner shafts
    /// around a core, as in a cathedral nave.
    /// </summary>
    public static void Column(MeshBuilder b, float height, float radius, int sides, int shafts, float plinth, float capital)
    {
        float width = radius * 2.6f;
        float bevel = radius * 0.12f;
        b.Variation = 0f;
        b.Box(new Vector3(0f, plinth * 0.5f, 0f), new Vector3(width, plinth, width), bevel);
        // A thinner step between plinth and shaft.
        float step = plinth * 0.3f;
        b.Box(new Vector3(0f, plinth + step * 0.5f, 0f), new Vector3(width * 0.84f, step, width * 0.84f), bevel);
        b.Variation = 0.5f;
        b.Box(new Vector3(0f, height - capital * 0.5f, 0f), new Vector3(width, capital, width), bevel);
        b.Box(new Vector3(0f, height - capital - step * 0.5f, 0f), new Vector3(width * 0.84f, step, width * 0.84f), bevel);

        float bottom = plinth + step, top = height - capital - step;
        bool smooth = sides > 12;
        if (shafts <= 1)
        {
            b.Variation = 0.25f;
            b.Sweep(SweepPath.Line(new Vector3(0f, bottom, 0f), new Vector3(0f, top, 0f), Vector3.UnitZ),
                Profile.Circle(radius, sides, smooth), capStart: false, capEnd: false);
            return;
        }

        float core = radius * 0.62f;
        b.Variation = 0.25f;
        b.Sweep(SweepPath.Line(new Vector3(0f, bottom, 0f), new Vector3(0f, top, 0f), Vector3.UnitZ),
            Profile.Circle(core, sides, smooth), capStart: false, capEnd: false);
        float thin = radius * 0.34f;
        for (int i = 0; i < shafts; i++)
        {
            float a = MathF.Tau * (i + 0.5f) / shafts;
            var offset = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (core + thin * 0.55f);
            b.Variation = 0.1f + 0.8f * i / shafts;
            b.Sweep(SweepPath.Line(new Vector3(0f, bottom, 0f) + offset, new Vector3(0f, top, 0f) + offset, Vector3.UnitZ),
                Profile.Circle(thin, Math.Max(8, sides / 2), smooth), capStart: false, capEnd: false);
        }
    }

    /// <summary>
    /// A pipe through <paramref name="points"/> with rounded bends, and a flange collar every
    /// <paramref name="flangeSpacing"/> meters (none if 0). The origin is the pipe's first point.
    /// </summary>
    public static void Pipe(MeshBuilder b, IReadOnlyList<Vector3> points, float radius, float bendRadius, float flangeSpacing)
    {
        List<PathFrame> path = SweepPath.Polyline(points, Vector3.UnitZ, bendRadius, 8);
        int sides = Math.Clamp((int)(radius * 60f), 10, 28);
        b.Variation = 0f;
        b.Sweep(path, Profile.Circle(radius, sides));

        if (flangeSpacing <= 0f)
        {
            return;
        }

        float length = SweepPath.Length(path);
        float collar = MathF.Max(0.05f, radius * 0.35f);
        int count = (int)MathF.Floor(length / flangeSpacing);
        float first = (length - (count - 1) * flangeSpacing) * 0.5f;
        Profile flange = Profile.Circle(radius * 1.28f, sides);
        for (int i = 0; i < count; i++)
        {
            float d = first + i * flangeSpacing;
            b.Variation = 0.3f + 0.6f * (i % 3) / 2f;
            b.Sweep(SweepPath.Slice(path, d - collar * 0.5f, d + collar * 0.5f), flange);
        }
    }

    /// <summary>
    /// Cables hanging from the origin to <paramref name="end"/>: <paramref name="strands"/> of them side by side,
    /// each sagging a little differently, the first by <paramref name="sag"/> meters.
    /// </summary>
    public static void Cables(MeshBuilder b, Vector3 end, float sag, float radius, int strands, int seed)
    {
        var random = new Random(seed);
        Vector3 across = Vector3.Normalize(Vector3.Cross(end.LengthSquared() > 1e-6f ? end : Vector3.UnitX, Vector3.UnitY));
        if (float.IsNaN(across.X))
        {
            across = Vector3.UnitZ;
        }

        for (int i = 0; i < Math.Max(1, strands); i++)
        {
            float spread = (i - (strands - 1) * 0.5f) * radius * 2.6f;
            Vector3 offset = across * spread + Vector3.UnitY * (float)(random.NextDouble() - 0.5) * radius * 2f;
            float strandSag = sag * (1f + 0.22f * i + 0.1f * (float)random.NextDouble());
            float strandRadius = radius * (0.75f + 0.5f * (float)random.NextDouble());
            b.Variation = (float)random.NextDouble();
            b.Sweep(SweepPath.Hanging(offset, end + offset, strandSag, 24), Profile.Circle(strandRadius, 8));
        }
    }

    /// <summary>
    /// An arch ring in the XY plane springing from (±span/2, 0): round up to rise = span/2, pointed above.
    /// <paramref name="thickness"/> is the ring's width in the arch's plane, <paramref name="depth"/> its
    /// extent along Z. <paramref name="flange"/> above 0 adds a thin inner fin as deep as a fraction of the
    /// ring (an iron rib rather than a stone arch).
    /// </summary>
    public static void Arch(MeshBuilder b, float span, float rise, float thickness, float depth, float flange, int segments)
    {
        Profile ring = Profile.Rect(thickness, depth, MathF.Min(thickness, depth) * 0.12f);
        b.Variation = 0f;
        foreach (List<PathFrame> half in SweepPath.Arch(span, rise, segments))
        {
            b.Sweep(half, ring);
        }

        if (flange <= 0f)
        {
            return;
        }

        // The fin hangs inside the ring: its center line runs on a smaller arch.
        float inset = thickness * 0.5f + flange * 0.5f;
        Profile fin = Profile.Rect(flange, depth * 0.3f, 0.01f);
        b.Variation = 0.6f;
        foreach (List<PathFrame> half in SweepPath.Arch(span, rise, segments))
        {
            var inner = new List<PathFrame>(half.Count);
            foreach (PathFrame f in half)
            {
                // Mitred stations stay on the mitre plane: move along their stretched side offset.
                float scale = f.SideOffset.Length();
                Vector3 direction = f.SideOffset / scale;
                inner.Add(f with { Position = f.Position - direction * (inset * scale) });
            }

            b.Sweep(inner, fin);
        }
    }

    /// <summary>
    /// A grate of <paramref name="size"/> (X wide, Y thick, Z deep) standing on the origin: a frame of two
    /// rails, bars across every <paramref name="spacing"/> meters, and cross-members underneath.
    /// </summary>
    public static void Grate(MeshBuilder b, Vector3 size, float spacing, float bar)
    {
        float hx = size.X * 0.5f, hz = size.Z * 0.5f;
        float rail = MathF.Min(0.14f, size.Z * 0.1f);
        float y = size.Y * 0.5f;
        b.Variation = 0f;
        b.Box(new Vector3(0f, y, hz - rail * 0.5f), new Vector3(size.X, size.Y, rail), 0.02f);
        b.Box(new Vector3(0f, y, -hz + rail * 0.5f), new Vector3(size.X, size.Y, rail), 0.02f);
        b.Variation = 0.35f;
        b.Box(new Vector3(-hx + rail * 0.5f, y, 0f), new Vector3(rail, size.Y, size.Z - 2f * rail), 0.02f);
        b.Box(new Vector3(hx - rail * 0.5f, y, 0f), new Vector3(rail, size.Y, size.Z - 2f * rail), 0.02f);

        float inner = size.X - 2f * rail;
        int bars = Math.Max(1, (int)MathF.Round(inner / spacing));
        float barHeight = size.Y * 0.55f;
        b.Variation = 0.7f;
        for (int i = 1; i < bars; i++)
        {
            float x = -hx + rail + inner * i / bars;
            b.Box(new Vector3(x, size.Y - barHeight * 0.5f, 0f), new Vector3(bar, barHeight, size.Z - 2f * rail), 0f);
        }

        // Cross-members under the bars, along X.
        b.Variation = 0.5f;
        for (int i = 1; i <= 2; i++)
        {
            float z = -hz + size.Z * i / 3f;
            b.Box(new Vector3(0f, size.Y * 0.25f, z), new Vector3(size.X - 2f * rail, size.Y * 0.5f, bar * 1.5f), 0f);
        }
    }
}
