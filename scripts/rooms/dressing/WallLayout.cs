using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>How a room's back wall is dressed (<see cref="WallDressing"/>).</summary>
public enum WallStyle
{
    /// <summary>A foundry's wall: iron pilasters, girders, grated furnace mouths glowing, pipes, chains hanging (volcanic).</summary>
    Foundry,

    /// <summary>Ancient machinery set in the wall: pistons, bands, wheels on hubs, panels of uneven height, glowing slits (the core).</summary>
    Machinery,

    /// <summary>An archive's wall: stone piers, iron shelves of server racks with gaps, icicles under the shelves (frozen).</summary>
    Archive,
}

/// <summary>The pieces a wall is dressed with; each style uses some of them.</summary>
public enum WallPiece
{
    Pilaster,
    Girder,
    Mouth,
    MouthGlow,
    Pipe,
    Chain,
    Panel,
    Wheel,
    Hub,
    Piston,
    Slit,
    Pier,
    Shelf,
    Rack,
    Icicles,
}

/// <summary>
/// One piece on a wall: its origin (meters, the wall's frame: X along it from its middle, Y up from its foot, Z out of
/// it toward the camera), its size where the piece is stretched (X, Y), and a roll about Z (radians).
/// </summary>
public readonly record struct WallPlacement(WallPiece Piece, Vector3 Position, Vector2 Size, float Roll = 0f);

/// <summary>
/// Pure, deterministic dressings for a room's back wall (<see cref="WallDressing"/> builds the meshes): bays between
/// pilasters, each filled with one motif, so a wide plain wall reads as a built thing at the scale of the room.
/// </summary>
public static class WallLayout
{
    /// <summary>Pilasters stand about this far apart, meters (the bays share the width evenly).</summary>
    public const float Bay = 7f;

    public const float PilasterWidth = 0.9f;

    public static List<WallPlacement> Build(WallStyle style, float width, float height, int seed) => style switch
    {
        WallStyle.Machinery => Machinery(width, height, seed),
        WallStyle.Archive => Archive(width, height, seed),
        _ => Foundry(width, height, seed),
    };

    /// <summary>The X of each pilaster, evenly spaced, the first and last at the wall's ends.</summary>
    public static List<float> Pilasters(float width)
    {
        int bays = Math.Max(1, (int)MathF.Round(width / Bay));
        var xs = new List<float>();
        for (int i = 0; i <= bays; i++)
        {
            xs.Add(-width * 0.5f + width * i / bays);
        }

        return xs;
    }

    private static float H(int seed, int a, int b) => DressingLayout.Hash01(seed + a * 7919, b);

    private static List<WallPlacement> Foundry(float width, float height, int seed)
    {
        var list = new List<WallPlacement>();
        List<float> xs = Pilasters(width);
        foreach (float x in xs)
        {
            list.Add(new WallPlacement(WallPiece.Pilaster, new Vector3(x, 0f, 0.35f), new Vector2(PilasterWidth, height)));
        }

        // Girders across the whole wall: one low, one high when the wall is tall.
        float low = MathF.Min(6f, height * 0.4f);
        list.Add(new WallPlacement(WallPiece.Girder, new Vector3(0f, low, 0.55f), new Vector2(width, 0.6f)));
        if (height > 14f)
        {
            list.Add(new WallPlacement(WallPiece.Girder, new Vector3(0f, height * 0.72f, 0.55f), new Vector2(width, 0.6f)));
        }

        for (int i = 0; i + 1 < xs.Count; i++)
        {
            float a = xs[i] + PilasterWidth * 0.5f, b = xs[i + 1] - PilasterWidth * 0.5f;
            float mid = (a + b) * 0.5f, span = b - a;
            float motif = H(seed, 1, i);
            if (motif < 0.4f && span > 3f)
            {
                // A furnace mouth on the girder: molten metal behind a grate.
                float w = MathF.Min(span * 0.6f, 4f), h = MathF.Min(w * 0.7f, MathF.Max(1.5f, height - low - 2f));
                list.Add(new WallPlacement(WallPiece.MouthGlow, new Vector3(mid, low + 0.4f, 0.05f), new Vector2(w, h)));
                list.Add(new WallPlacement(WallPiece.Mouth, new Vector3(mid, low + 0.4f, 0.2f), new Vector2(w, h)));
            }
            else if (motif < 0.75f)
            {
                // Pipes running up the bay, one or two.
                int n = 1 + (int)(H(seed, 2, i) * 2f);
                for (int k = 0; k < n; k++)
                {
                    float px = a + span * (k + 1f) / (n + 1f);
                    list.Add(new WallPlacement(WallPiece.Pipe, new Vector3(px, 0f, 0.4f), new Vector2(0.28f + 0.12f * H(seed, 3, i * 3 + k), height)));
                }
            }

            // Chains hanging from the top in some bays.
            if (H(seed, 4, i) < 0.45f)
            {
                float length = height * (0.25f + 0.45f * H(seed, 5, i));
                list.Add(new WallPlacement(WallPiece.Chain, new Vector3(a + span * (0.3f + 0.4f * H(seed, 6, i)), height, 0.9f), new Vector2(0.07f, length)));
            }
        }

        return list;
    }

    /// <summary>An archive's shelves stand this far apart, meters; a rack on them is <see cref="RackPitch"/> wide.</summary>
    public const float ShelfPitch = 3.2f;

    public const float RackPitch = 1.1f;

    private static List<WallPlacement> Archive(float width, float height, int seed)
    {
        var list = new List<WallPlacement>();
        List<float> xs = Pilasters(width);
        foreach (float x in xs)
        {
            list.Add(new WallPlacement(WallPiece.Pier, new Vector3(x, 0f, 0.4f), new Vector2(1.2f, height)));
        }

        int shelves = Math.Max(1, (int)((height - 0.5f) / ShelfPitch));
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            float a = xs[i] + 0.7f, b = xs[i + 1] - 0.7f;
            float span = b - a, mid = (a + b) * 0.5f;
            int racks = Math.Max(1, (int)(span / RackPitch));
            float pitch = span / racks;
            for (int j = 0; j < shelves; j++)
            {
                // A fallen shelf now and then leaves a gap of bare wall.
                if (j > 0 && H(seed, 1 + j, i) < 0.1f)
                {
                    continue;
                }

                float y = j * ShelfPitch;
                if (j > 0)
                {
                    list.Add(new WallPlacement(WallPiece.Shelf, new Vector3(mid, y, 0.5f), new Vector2(span + 0.3f, 0.18f)));
                    if (H(seed, 20 + j, i) < 0.6f)
                    {
                        list.Add(new WallPlacement(WallPiece.Icicles, new Vector3(mid, y - 0.09f, 0.75f), new Vector2(span * 0.9f, 0.6f + 0.5f * H(seed, 40 + j, i))));
                    }
                }

                for (int k = 0; k < racks; k++)
                {
                    if (H(seed, 100 + j, i * 37 + k) < 0.18f)
                    {
                        continue; // looted: a gap in the row
                    }

                    list.Add(new WallPlacement(WallPiece.Rack, new Vector3(a + (k + 0.5f) * pitch, y + (j > 0 ? 0.09f : 0f), 0.15f),
                        new Vector2(pitch * 0.92f, ShelfPitch - 0.5f), Roll: (int)(H(seed, 200 + j, i * 37 + k) * 3f)));
                }
            }
        }

        return list;
    }

    private static List<WallPlacement> Machinery(float width, float height, int seed)
    {
        var list = new List<WallPlacement>();
        List<float> xs = Pilasters(width);
        for (int k = 0; k < xs.Count; k++)
        {
            // Pistons on the bay lines, some cut short (the machine stopped mid-stroke).
            float tall = H(seed, 7, k) < 0.3f ? height * (0.45f + 0.3f * H(seed, 8, k)) : height;
            list.Add(new WallPlacement(WallPiece.Piston, new Vector3(xs[k], 0f, 0.4f), new Vector2(0.45f, tall)));
        }

        // A band across the wall, two on a tall one: the frame the machinery hangs in.
        float band = MathF.Min(5f, height * 0.3f);
        list.Add(new WallPlacement(WallPiece.Girder, new Vector3(0f, band, 0.55f), new Vector2(width, 0.5f)));
        if (height > 16f)
        {
            list.Add(new WallPlacement(WallPiece.Girder, new Vector3(0f, height * 0.78f, 0.55f), new Vector2(width, 0.5f)));
        }

        for (int i = 0; i + 1 < xs.Count; i++)
        {
            float a = xs[i] + 0.5f, b = xs[i + 1] - 0.5f;
            float mid = (a + b) * 0.5f, span = b - a;
            if (H(seed, 2, i) < 0.5f)
            {
                // A wheel on a hub, above the band; a glowing slit under it.
                float r = MathF.Min(span * 0.45f, (height - band) * 0.32f) * (0.75f + 0.25f * H(seed, 3, i));
                float y = band + 0.5f + r + (height - band - 1f - 2f * r) * H(seed, 4, i);
                list.Add(new WallPlacement(WallPiece.Wheel, new Vector3(mid, y, 0.5f), new Vector2(r, r), H(seed, 5, i) * MathF.PI));
                list.Add(new WallPlacement(WallPiece.Hub, new Vector3(mid, y, 0.3f), new Vector2(r * 0.2f, r * 0.2f)));
                list.Add(new WallPlacement(WallPiece.Slit, new Vector3(mid, band - 1f, 0.25f), new Vector2(span * 0.6f, 0.14f)));
            }
            else
            {
                // Panels of uneven height stacked up the bay, gaps of bare wall between; a slit along some.
                float y = 0.6f;
                int n = 0;
                while (y < height - 1.5f && n < 6)
                {
                    float h = 1.6f + 3.4f * H(seed, 10 + n, i);
                    h = MathF.Min(h, height - 0.8f - y);
                    float w = span * (0.55f + 0.4f * H(seed, 20 + n, i));
                    if (H(seed, 30 + n, i) > 0.25f)
                    {
                        list.Add(new WallPlacement(WallPiece.Panel, new Vector3(mid, y + h * 0.5f, 0.1f), new Vector2(w, h)));
                        if (H(seed, 40 + n, i) < 0.35f)
                        {
                            list.Add(new WallPlacement(WallPiece.Slit, new Vector3(mid, y + h * 0.5f, 0.25f), new Vector2(w * 0.8f, 0.12f)));
                        }
                    }

                    y += h + 0.4f + 1.2f * H(seed, 50 + n, i);
                    n++;
                }
            }
        }

        return list;
    }
}
