using System;
using System.Globalization;

namespace TheDeep;

/// <summary>File naming for owner captures (screenshots, videos). Pure logic, unit tested.</summary>
public static class CaptureFiles
{
    /// <summary>Sortable local time stamp used as a capture's base name: 2026-10-24_14-03-22.</summary>
    public static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// The stamp for <paramref name="time"/>, or the stamp with a _2, _3… suffix when
    /// <paramref name="taken"/> says that base name is already used.
    /// </summary>
    public static string UniqueBaseName(DateTime time, Func<string, bool> taken)
    {
        string stamp = Stamp(time);
        if (!taken(stamp))
        {
            return stamp;
        }

        for (int i = 2; ; i++)
        {
            string candidate = $"{stamp}_{i}";
            if (!taken(candidate))
            {
                return candidate;
            }
        }
    }
}
