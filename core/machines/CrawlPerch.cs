using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Where a crawling machine clings (<see cref="CrawlMotion"/>): the nearest metal block to where it spawns, and the faces of that
/// block nothing else touches. The machine feeds it boxes and asks the engine only whether each probe meets anything.
/// </summary>
public static class CrawlPerch
{
    /// <summary>How far a probe stands off its face (its center), meters; also the probe's thickness.</summary>
    public const float FaceGap = 0.1f;

    /// <summary>How much shorter than its face a probe is, so a neighbour only touching a corner leaves the face free, meters.</summary>
    public const float CornerInset = 0.2f;

    /// <summary>How far above its face a crawler's path runs, meters.</summary>
    public const float Standoff = 0.01f;

    /// <summary>The faces of a box, in the order <see cref="SurfacePath.AroundBox"/> takes them: bottom, right, top, left.</summary>
    public const int Faces = 4;

    /// <summary>
    /// The index of the metal box nearest to <paramref name="spawn"/> and within <paramref name="search"/> meters of it (a tie goes
    /// to the later box), or -1 if there is none.
    /// </summary>
    public static int Nearest(IReadOnlyList<PerchBox> boxes, Vector2 spawn, float search)
    {
        int found = -1;
        float best = search;
        for (int i = 0; i < boxes.Count; i++)
        {
            if (!boxes[i].Metal)
            {
                continue;
            }

            Vector2 center = (boxes[i].Min + boxes[i].Max) * 0.5f;
            Vector2 half = (boxes[i].Max - boxes[i].Min) * 0.5f;
            float dx = MathF.Max(MathF.Abs(spawn.X - center.X) - half.X, 0f);
            float dy = MathF.Max(MathF.Abs(spawn.Y - center.Y) - half.Y, 0f);
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (distance <= best)
            {
                best = distance;
                found = i;
            }
        }

        return found;
    }

    /// <summary>The probe of face <paramref name="face"/> (0 bottom, 1 right, 2 top, 3 left) of <paramref name="box"/>.</summary>
    public static FaceProbe Probe(PerchBox box, int face)
    {
        Vector2 middle = (box.Min + box.Max) * 0.5f;
        float along = (face % 2 == 0 ? box.Max.X - box.Min.X : box.Max.Y - box.Min.Y) - CornerInset;
        return face switch
        {
            0 => new FaceProbe(new Vector2(middle.X, box.Min.Y - FaceGap), new Vector2(along, FaceGap)),
            1 => new FaceProbe(new Vector2(box.Max.X + FaceGap, middle.Y), new Vector2(FaceGap, along)),
            2 => new FaceProbe(new Vector2(middle.X, box.Max.Y + FaceGap), new Vector2(along, FaceGap)),
            _ => new FaceProbe(new Vector2(box.Min.X - FaceGap, middle.Y), new Vector2(FaceGap, along)),
        };
    }

    /// <summary>The path around <paramref name="box"/> over the faces <paramref name="free"/> says are clear (bottom, right, top, left); null if none is.</summary>
    public static SurfacePath? Path(PerchBox box, IReadOnlyList<bool> free) => SurfacePath.AroundBox(box.Min, box.Max, free, Standoff);
}
