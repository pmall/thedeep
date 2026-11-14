using System;

namespace TheDeep;

/// <summary>
/// Pure: how wide to set a text that wraps, so its rows come out even. Wrapped at the widest a box allows, a line that just overflows
/// one row leaves a word or two alone on the next; set at the narrowest width that still takes no more rows, every row is about as
/// long as the others. Used by the boxes that wrap what they say (the banner, the dialogue box). Widths in pixels.
/// </summary>
public static class EvenRows
{
    /// <summary>
    /// The width to set a text at, given how many rows it takes at a width (<paramref name="rowsAt"/>, as the box's font wraps it), its
    /// width on one row (<paramref name="natural"/>), and the widest the box allows (<paramref name="widest"/>): its natural width when
    /// that fits on one row, else the narrowest width, to within <paramref name="step"/>, at which it takes no more rows than at the widest.
    /// </summary>
    public static float Width(Func<float, int> rowsAt, float natural, float widest, float step = 1f)
    {
        if (natural <= widest)
        {
            return natural;
        }

        int rows = rowsAt(widest);
        // Narrower than its length shared out over those rows, it cannot fit in them.
        float tooNarrow = natural / rows - step;
        float fits = widest;
        while (fits - tooNarrow > step)
        {
            float mid = 0.5f * (tooNarrow + fits);
            if (rowsAt(mid) <= rows)
            {
                fits = mid;
            }
            else
            {
                tooNarrow = mid;
            }
        }

        return fits;
    }
}
