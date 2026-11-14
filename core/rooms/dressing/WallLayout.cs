using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>How a room's back wall is dressed (<see cref="WallDressing"/>).</summary>
public enum WallStyle
{
    /// <summary>A foundry's wall: iron pilasters, girders, grated furnace mouths glowing, pipes, chains hanging (volcanic).</summary>
    Foundry,

    /// <summary>
    /// The deep works of the machine's cathedral: pistons on the bay lines, bands across, trains of meshed gears on their axles,
    /// banks of organ pipes stacked up the bays, glowing slits (the core).
    /// </summary>
    Machinery,

    /// <summary>An archive's wall: stone piers, iron shelves of server racks with gaps, icicles under the shelves (frozen).</summary>
    Archive,

    /// <summary>
    /// A cathedral's wall: a plinth, stone pilasters carrying a blind arcade of pointed arches (a rose in some tympana),
    /// string courses, tiers of lancet niches above, the odd arch fallen (flooded; the frozen layer's chapels).
    /// </summary>
    Nave,
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

    /// <summary>A bank of organ pipes on a wind chest, standing on its foot (Size: width, height; Position its foot's middle, on its back).</summary>
    Bank,

    /// <summary>A toothed gear on the wall (Size: its radius, twice; Position its middle, on its back; Roll its turn).</summary>
    Gear,

    /// <summary>An axle's end standing out of a gear (Size: its radius, twice; Position its middle, on its back).</summary>
    Hub,
    Piston,
    Slit,
    Pier,
    Shelf,
    Rack,
    Icicles,

    /// <summary>A stone band along the wall's foot (Size: length, height).</summary>
    Plinth,

    /// <summary>A stone pilaster from its foot (Size: width, height).</summary>
    Shaft,

    /// <summary>A stone block a shaft carries (Size: width, height; Position its center).</summary>
    Capital,

    /// <summary>A stone arch ring from its springing line's middle (Size: span, rise; pointed when the rise passes half the span).</summary>
    Arch,

    /// <summary>The shadowed back of a niche or a blind arch, flush on the wall (Size: width, height; Position its center).</summary>
    Recess,

    /// <summary>A stone string course along the wall (Size: length, height; Position its center).</summary>
    Course,

    /// <summary>A rose in a tympanum: a stone ring and its tracery over a recess (Size: radius, radius; Roll: the tracery's turn).</summary>
    Rose,
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

    /// <summary>A pilaster's width along the wall, meters; a bay's motif fits between two of them.</summary>
    public const float PilasterWidth = 0.9f;

    /// <summary>
    /// The wall's pieces. <paramref name="frost"/> (0 to 1) is the share of a nave's bays with icicles under their courses;
    /// the other styles ignore it.
    /// </summary>
    public static List<WallPlacement> Build(WallStyle style, float width, float height, int seed, float frost = 0f) => style switch
    {
        WallStyle.Machinery => Machinery(width, height, seed),
        WallStyle.Archive => Archive(width, height, seed),
        WallStyle.Nave => Nave(width, height, seed, frost),
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

    /// <summary>The width a server rack takes on its shelf, meters.</summary>
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

    /// <summary>A nave's plinth height and its arcade pilasters' width, meters.</summary>
    public const float NavePlinth = 0.8f;

    /// <summary>The width of a nave's arcade pilaster, meters.</summary>
    public const float NavePilaster = 1f;

    private static List<WallPlacement> Nave(float width, float height, int seed, float frost)
    {
        const float capital = 0.4f, course = 0.45f, ring = 0.45f, upperShaft = 0.6f;
        var list = new List<WallPlacement>();
        List<float> xs = Pilasters(width);
        float gap = xs.Count > 1 ? xs[1] - xs[0] : width;
        list.Add(new WallPlacement(WallPiece.Plinth, new Vector3(0f, 0f, 0.4f), new Vector2(width, NavePlinth)));

        // The arcade: as tall as the wall allows under a pointed arch, its course and a margin.
        float span = gap - NavePilaster + ring;
        float rise = span * 0.62f;
        float shaft = Math.Clamp(MathF.Min(height * 0.32f, height - NavePlinth - capital - rise - course - 0.5f), 2f, 6.5f);
        float foot = NavePlinth;
        for (int tier = 0; foot + shaft + capital + rise + course <= height + 1e-3f; tier++)
        {
            bool arcade = tier == 0;
            float pilaster = arcade ? NavePilaster : upperShaft;
            float spring = foot + shaft + capital;
            foreach (float x in xs)
            {
                list.Add(new WallPlacement(WallPiece.Shaft, new Vector3(x, foot, 0.25f), new Vector2(pilaster, shaft)));
                list.Add(new WallPlacement(WallPiece.Capital, new Vector3(x, foot + shaft + capital * 0.5f, 0.4f), new Vector2(pilaster + 0.4f, capital)));
            }

            float top = spring + rise;
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float mid = (xs[i] + xs[i + 1]) * 0.5f;
                float inner = xs[i + 1] - xs[i] - pilaster;
                if (arcade)
                {
                    // A blind arch over a shadowed bay; a rose in some tympana; now and then the arch has fallen.
                    list.Add(new WallPlacement(WallPiece.Recess, new Vector3(mid, (foot + spring) * 0.5f, 0.04f), new Vector2(inner, spring - foot)));
                    if (H(seed, 1, i) >= 0.08f)
                    {
                        list.Add(new WallPlacement(WallPiece.Arch, new Vector3(mid, spring, 0.25f), new Vector2(span, rise)));
                        float r = MathF.Min(inner * 0.16f, rise * 0.22f);
                        if (H(seed, 2, i) < 0.5f && r > 0.35f)
                        {
                            list.Add(new WallPlacement(WallPiece.Rose, new Vector3(mid, spring + rise * 0.45f, 0.15f), new Vector2(r, r), H(seed, 3, i) * MathF.PI));
                        }
                    }
                }
                else
                {
                    // Lancet niches, one to three to a bay, each on its own two jambs; the odd one walled up.
                    int n = inner > 5f ? 3 : inner > 2.6f ? 2 : 1;
                    float lancet = MathF.Min(1.3f, inner / (n * 1.9f));
                    for (int k = 0; k < n; k++)
                    {
                        if (H(seed, 10 + tier, i * 7 + k) < 0.12f)
                        {
                            continue;
                        }

                        float lx = xs[i] + pilaster * 0.5f + inner * (k + 0.5f) / n;
                        float jamb = 0.18f;
                        list.Add(new WallPlacement(WallPiece.Recess, new Vector3(lx, (foot + spring) * 0.5f + 0.2f, 0.04f), new Vector2(lancet, spring - foot - 0.4f)));
                        list.Add(new WallPlacement(WallPiece.Shaft, new Vector3(lx - (lancet + jamb) * 0.5f, foot + 0.4f, 0.15f), new Vector2(jamb, spring - foot - 0.4f)));
                        list.Add(new WallPlacement(WallPiece.Shaft, new Vector3(lx + (lancet + jamb) * 0.5f, foot + 0.4f, 0.15f), new Vector2(jamb, spring - foot - 0.4f)));
                        list.Add(new WallPlacement(WallPiece.Arch, new Vector3(lx, spring, 0.15f), new Vector2(lancet + jamb, (lancet + jamb) * 0.75f)));
                    }
                }
            }

            list.Add(new WallPlacement(WallPiece.Course, new Vector3(0f, top + course * 0.5f, 0.35f), new Vector2(width, course)));
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                if (H(seed, 20 + tier, i) < frost)
                {
                    float mid = (xs[i] + xs[i + 1]) * 0.5f, inner = xs[i + 1] - xs[i] - pilaster;
                    list.Add(new WallPlacement(WallPiece.Icicles, new Vector3(mid, top, 0.6f), new Vector2(inner * 0.85f, 0.5f + 0.6f * H(seed, 30 + tier, i))));
                }
            }

            // The tiers above: slimmer shafts, lancets under a lower arch.
            foot = top + course;
            shaft = Math.Clamp(height * 0.18f, 2.4f, 4f);
            span = MathF.Min(gap, 3f);
            rise = 1.2f;
        }

        return list;
    }

    /// <summary>A gear's teeth stand this far apart along its pitch circle, meters, so meshed gears share their teeth's size.</summary>
    public const float ToothPitch = 0.5f;

    /// <summary>Two gears mesh with their middles this share of their radii's sum apart (their pitch circles touching).</summary>
    public const float GearMesh = 0.92f;

    /// <summary>How deep a machinery wall's bank stands out from its back, meters.</summary>
    public const float BankDepth = 0.7f;

    /// <summary>The wind chest's height under a bank <paramref name="height"/> meters tall, meters.</summary>
    public static float BankChest(float height) => Math.Clamp(height * 0.16f, 0.35f, 0.7f);

    /// <summary>How many teeth a gear of <paramref name="radius"/> meters has.</summary>
    public static int GearTeeth(float radius) => Math.Max(10, (int)MathF.Round(MathF.Tau * GearMesh * radius / ToothPitch));

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
                GearTrain(list, seed, i, mid, span, band, height);
                // Under the band, a low bank now and then, and a slit of light.
                if (H(seed, 6, i) < 0.6f && band > 2.4f)
                {
                    float w = span * (0.5f + 0.35f * H(seed, 9, i));
                    list.Add(new WallPlacement(WallPiece.Bank, new Vector3(mid, 0.3f, 0.05f), new Vector2(w, band - 0.9f)));
                }
                else
                {
                    list.Add(new WallPlacement(WallPiece.Slit, new Vector3(mid, band - 1f, 0.25f), new Vector2(span * 0.6f, 0.14f)));
                }
            }
            else
            {
                // Banks of uneven height stacked up the bay, gaps of bare wall between; a slit of light along some wind chests.
                float y = 0.3f;
                int n = 0;
                while (y < height - 1.8f && n < 6)
                {
                    float h = 2.2f + 3.2f * H(seed, 10 + n, i);
                    h = MathF.Min(h, height - 0.6f - y);
                    float w = span * (0.6f + 0.35f * H(seed, 20 + n, i));
                    if (H(seed, 30 + n, i) > 0.2f && h > 1.4f)
                    {
                        list.Add(new WallPlacement(WallPiece.Bank, new Vector3(mid, y, 0.05f), new Vector2(w, h)));
                        if (H(seed, 40 + n, i) < 0.35f)
                        {
                            list.Add(new WallPlacement(WallPiece.Slit, new Vector3(mid, y + BankChest(h) * 0.5f, 0.05f + BankDepth + 0.03f), new Vector2(w * 0.7f, 0.1f)));
                        }
                    }

                    y += h + 0.5f + 1.2f * H(seed, 50 + n, i);
                    n++;
                }
            }
        }

        return list;
    }

    /// <summary>
    /// A great gear over the band, its axle's end at its middle, and a smaller one meshed with it above or below, turned so
    /// their teeth interleave; the smaller is left out where neither side has room in the bay.
    /// </summary>
    private static void GearTrain(List<WallPlacement> list, int seed, int bay, float mid, float span, float band, float height)
    {
        float r = MathF.Min(span * 0.45f, (height - band) * 0.32f) * (0.75f + 0.25f * H(seed, 3, bay));
        float y = band + 0.5f + r + (height - band - 1f - 2f * r) * H(seed, 4, bay);
        float rp = r * (0.38f + 0.17f * H(seed, 11, bay));
        float d = GearMesh * (r + rp);
        float lean = (0.12f + 0.2f * H(seed, 12, bay)) * MathF.PI * (H(seed, 13, bay) < 0.5f ? -1f : 1f);
        float? toward = null;
        foreach (float up in H(seed, 14, bay) < 0.5f ? new[] { 1f, -1f } : new[] { -1f, 1f })
        {
            float angle = up * MathF.PI * 0.5f + lean;
            var c = new Vector2(mid + d * MathF.Cos(angle), y + d * MathF.Sin(angle));
            if (MathF.Abs(c.X - mid) + rp <= span * 0.5f + 0.3f && c.Y - rp >= band + 0.6f && c.Y + rp <= height - 0.3f)
            {
                toward = angle;
                break;
            }
        }

        // A tooth of the great gear on the line between the middles, a gap of the small one facing it.
        float step = MathF.Tau / GearTeeth(r);
        float theta = toward ?? H(seed, 5, bay) * MathF.Tau;
        list.Add(new WallPlacement(WallPiece.Gear, new Vector3(mid, y, 0.3f), new Vector2(r, r), theta - 0.27f * step));
        list.Add(new WallPlacement(WallPiece.Hub, new Vector3(mid, y, 0.3f), new Vector2(r * 0.12f, r * 0.12f)));
        if (toward is float angle2)
        {
            float pstep = MathF.Tau / GearTeeth(rp);
            var at = new Vector3(mid + d * MathF.Cos(angle2), y + d * MathF.Sin(angle2), 0.38f);
            list.Add(new WallPlacement(WallPiece.Gear, at, new Vector2(rp, rp), angle2 + MathF.PI - 0.77f * pstep));
            list.Add(new WallPlacement(WallPiece.Hub, at, new Vector2(rp * 0.15f, rp * 0.15f)));
        }
    }
}
