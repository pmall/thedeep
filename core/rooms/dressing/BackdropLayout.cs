using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// Pure, deterministic layouts of the architecture behind a room (<see cref="BackdropArchitecture"/> turns them into meshes):
/// regular bays with the odd broken pier or fallen arch, so the backdrop reads as built structures receding into the
/// haze rather than scattered boxes. Each style is rules over its spec (<see cref="NaveBackdropSpec"/>,
/// <see cref="StacksBackdropSpec"/>, <see cref="FoundryBackdropSpec"/>, <see cref="MachineryBackdropSpec"/>); the piece
/// sizes they assume are in <see cref="BackdropDims"/>. This class picks the style and holds what they share.
/// </summary>
public static class BackdropLayout
{
    /// <summary>A style's band is at least this many meters tall per unit of the row's scale (a few bays' worth).</summary>
    public const float BandPerScale = 10f;

    /// <summary>The salt of the bays' phase.</summary>
    private const int PhaseSalt = 999;

    /// <summary>The pieces of one row of the given style, placed in the row's frame; the same row always gives the same
    /// pieces.</summary>
    public static List<BackdropPlacement> Build(BackdropStyle style, BackdropRow row) => style switch
    {
        BackdropStyle.Nave => NaveBackdrop.Build(row),
        BackdropStyle.Stacks => StacksBackdrop.Build(row),
        BackdropStyle.Foundry => FoundryBackdrop.Build(row),
        BackdropStyle.Machinery => MachineryBackdrop.Build(row),
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "no layout for this backdrop style"),
    };

    /// <summary>The bay lines of a row: X of each pier, a seeded phase so rows don't line up.</summary>
    public static List<float> Bays(BackdropRow row)
    {
        var xs = new List<float>();
        float phase = DressingLayout.Hash01(row.Seed, PhaseSalt) * row.Pitch;
        for (float x = row.MinX - phase; x <= row.MaxX + row.Pitch; x += row.Pitch)
        {
            xs.Add(x);
        }

        return xs;
    }

    /// <summary>The band a style places its features in: the room's bounds, at least a few bays' worth tall.</summary>
    public static float Band(BackdropRow row) => MathF.Max(row.RoofY - row.FloorY, BandPerScale * row.Scale);
}
