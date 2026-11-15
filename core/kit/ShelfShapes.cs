using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The kit's shelving and organ pieces as pure geometry (Godot-free, unit tested): a stack of shelves, a bank of organ pipes
/// on a wind chest, a server rack. Same conventions as <see cref="KitShapes"/>: the origin is the middle of the footprint on the floor.
/// </summary>
public static class ShelfShapes
{
    /// <summary>
    /// A stack of shelves of <paramref name="size"/> standing on the origin, front facing +Z, cheap enough to repeat by the
    /// hundred far away (the archive towers behind a room): a recessed body, a lip every <paramref name="shelf"/> meters, a
    /// divider down the middle and posts at both sides. A few dozen triangles where a <see cref="Rack"/> has thousands.
    /// </summary>
    public static void Stack(MeshBuilder b, Vector3 size, float shelf)
    {
        float hx = size.X * 0.5f, hz = size.Z * 0.5f;
        float post = MathF.Min(0.25f, size.X * 0.08f);
        b.Variation = 0.2f;
        b.Box(new Vector3(0f, size.Y * 0.5f, -hz * 0.3f), new Vector3(size.X - 2f * post, size.Y, size.Z * 0.7f), 0f);
        b.Variation = 0f;
        b.Box(new Vector3(-hx + post * 0.5f, size.Y * 0.5f, 0f), new Vector3(post, size.Y, size.Z), post * 0.15f);
        b.Box(new Vector3(hx - post * 0.5f, size.Y * 0.5f, 0f), new Vector3(post, size.Y, size.Z), post * 0.15f);
        b.Variation = 0.6f;
        b.Box(new Vector3(0f, size.Y * 0.5f, hz * 0.2f), new Vector3(post * 0.5f, size.Y, size.Z * 0.6f), 0f);
        int shelves = Math.Max(1, (int)MathF.Round(size.Y / shelf));
        for (int i = 1; i <= shelves; i++)
        {
            float y = size.Y * i / shelves;
            b.Variation = 0.3f + 0.6f * i / shelves;
            b.Box(new Vector3(0f, y - 0.06f, 0f), new Vector3(size.X, 0.12f, size.Z), 0f);
        }
    }

    /// <summary>
    /// A bank of the old machine's pipes of <paramref name="size"/> standing on the origin, front facing +Z: a wind chest
    /// <paramref name="chest"/> meters tall across its foot, a post up each side, and between them a rank of organ pipes
    /// (<see cref="OrganPipeShape"/>) standing on the chest, their heights graded in a flat that rises to its middle, falls
    /// to it, or climbs to one side, by <paramref name="seed"/>. Organ pipes, so a wall of banks reads as the works of the
    /// machine's cathedral, never as shelves or windows.
    /// </summary>
    public static void Bank(MeshBuilder b, Vector3 size, float chest, int seed)
    {
        var random = new Random(seed);
        float hx = size.X * 0.5f;
        float post = MathF.Min(0.2f, size.X * 0.06f), cap = post * 0.8f;
        b.Variation = 0f;
        b.Box(new Vector3(0f, chest * 0.5f, 0f), new Vector3(size.X, chest, size.Z), chest * 0.08f);
        // A moulding along the chest's top, standing a little proud of its front.
        b.Box(new Vector3(0f, chest - 0.05f, 0.04f), new Vector3(size.X + 0.08f, 0.1f, size.Z + 0.08f), 0.02f);
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s * (hx - post * 0.5f);
            b.Variation = 0.5f;
            b.Box(new Vector3(x, chest + (size.Y - chest - cap) * 0.5f, -0.04f), new Vector3(post, size.Y - chest - cap, size.Z * 0.8f), post * 0.1f);
            b.Box(new Vector3(x, size.Y - cap * 0.5f, -0.04f), new Vector3(post * 1.5f, cap, size.Z * 0.9f), cap * 0.15f);
        }

        float inner = size.X - 2f * post - 0.1f;
        int count = Math.Max(3, (int)MathF.Round(inner / 0.42f));
        float spacing = inner / count;
        float reach = size.Y - chest - 0.05f;
        int flat = random.Next(3);
        bool rising = random.NextDouble() < 0.5;
        var heights = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? i / (count - 1f) : 0.5f;
            float grade = flat switch
            {
                0 => 1f - MathF.Abs(2f * t - 1f), // a mitre: tallest in the middle
                1 => MathF.Abs(2f * t - 1f), // a vee: tallest at the posts
                _ => rising ? t : 1f - t,
            };
            heights[i] = reach * (0.55f + 0.45f * grade);
        }

        Matrix4x4 placement = b.Transform;
        b.Transform = Matrix4x4.CreateTranslation(0f, chest, 0f) * placement;
        // As wide as the spacing allows, but never so wide that the shortest pipe has no room for its foot and mouth.
        float radius = MathF.Min(spacing * 0.4f, reach * 0.55f / 8f);
        OrganPipeShape.Rank(b, heights, spacing, radius);
        b.Transform = placement;
    }

    /// <summary>
    /// A server rack of <paramref name="size"/> standing on the origin, front facing +Z: a frame, a back
    /// panel and <paramref name="units"/> drive bays, some pulled out or missing (drowned and looted).
    /// </summary>
    public static void Rack(MeshBuilder b, Vector3 size, int units, int seed)
    {
        var random = new Random(seed);
        float hx = size.X * 0.5f, hz = size.Z * 0.5f;
        float post = MathF.Min(0.12f, size.X * 0.1f);
        float bevel = 0.025f;
        b.Variation = 0f;
        // Posts, top and plinth, back panel and side panels.
        b.Box(new Vector3(-hx + post * 0.5f, size.Y * 0.5f, 0f), new Vector3(post, size.Y, size.Z), bevel);
        b.Box(new Vector3(hx - post * 0.5f, size.Y * 0.5f, 0f), new Vector3(post, size.Y, size.Z), bevel);
        float top = 0.14f, plinth = 0.18f;
        b.Box(new Vector3(0f, size.Y - top * 0.5f, 0f), new Vector3(size.X, top, size.Z), bevel);
        b.Box(new Vector3(0f, plinth * 0.5f, 0f), new Vector3(size.X - 0.04f, plinth, size.Z - 0.04f), bevel);
        b.Variation = 0.2f;
        b.Box(new Vector3(0f, size.Y * 0.5f, -hz + 0.04f), new Vector3(size.X - 2f * post, size.Y - top, 0.08f), 0.01f);

        float inner = size.X - 2f * post;
        float bayHeight = (size.Y - top - plinth) / Math.Max(1, units);
        float bayDepth = size.Z * 0.8f;
        for (int i = 0; i < units; i++)
        {
            double roll = random.NextDouble();
            if (roll < 0.12)
            {
                continue; // missing: the back panel shows through
            }

            float pulled = roll < 0.24 ? 0.15f + 0.35f * (float)random.NextDouble() : 0.01f * (float)random.NextDouble();
            float y = plinth + bayHeight * (i + 0.5f);
            float front = hz - 0.03f + pulled;
            b.Variation = (float)random.NextDouble();
            b.Box(new Vector3(0f, y, front - bayDepth * 0.5f), new Vector3(inner - 0.02f, bayHeight - 0.03f, bayDepth), 0.012f);
            // A handle on each side of the bay front.
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(new Vector3(side * (inner * 0.5f - 0.07f), y, front + 0.02f), new Vector3(0.025f, bayHeight * 0.55f, 0.04f), 0.006f);
            }
        }
    }
}
