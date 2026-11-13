using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// One row of the architecture behind a room, as a world's layer or a room states it: how far behind the play plane it
/// stands (<see cref="Depth"/>, meters), its bay (<see cref="Pitch"/>, meters) and the size of its pieces
/// (<see cref="Scale"/>, 1 for the nearest row's), so far rows read as larger structures. <see cref="Defaults"/> when
/// neither says.
/// </summary>
public readonly record struct BackdropRowSpec(float Depth, float Pitch, float Scale)
{
    /// <summary>Three rows, nearest first: 15, 24 and 36 m behind the play plane.</summary>
    public static readonly IReadOnlyList<BackdropRowSpec> Defaults = [new(15f, 9f, 1f), new(24f, 12f, 1.3f), new(36f, 15f, 1.7f)];

    /// <summary>
    /// This row behind a room whose camera bounds run from <paramref name="min"/> to <paramref name="max"/>: wide and tall
    /// enough to fill, at its depth, the view of a camera up to <paramref name="viewDistance"/> meters in front of the play
    /// plane (<paramref name="halfWidthPerMeter"/> and <paramref name="halfHeightPerMeter"/> are the view's half extent per
    /// meter from the camera), so its ends, foot and top stay off screen; its pieces stand in the band of the bounds.
    /// </summary>
    public BackdropRow Behind(Vector2 min, Vector2 max, float viewDistance, float halfWidthPerMeter, float halfHeightPerMeter, int seed)
    {
        float reach = viewDistance + Depth;
        float sideways = reach * halfWidthPerMeter;
        float upAndDown = reach * halfHeightPerMeter;
        return new BackdropRow(min.X - sideways, max.X + sideways, min.Y - upAndDown, max.Y + upAndDown, min.Y, max.Y, Pitch, Scale, seed);
    }
}
