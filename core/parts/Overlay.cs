using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// How a derived part assembly lays its own entries over its base's (<c>scripts/parts/PartAssembly.cs</c>): an entry
/// with the same key as a base entry replaces it where it stands, any other is appended. Godot-free.
/// </summary>
public static class Overlay
{
    /// <summary>
    /// <paramref name="inherited"/> with each entry of <paramref name="own"/> laid over it. An entry whose key is
    /// null or empty (<paramref name="keyOf"/>) is never a replacement: it is always appended.
    /// </summary>
    public static List<T> Merge<T>(IEnumerable<T> inherited, IEnumerable<T> own, Func<T, string?> keyOf)
    {
        var merged = new List<T>(inherited);
        foreach (T entry in own)
        {
            string? key = keyOf(entry);
            int at = string.IsNullOrEmpty(key) ? -1 : merged.FindIndex(m => keyOf(m) == key);
            if (at >= 0)
            {
                merged[at] = entry;
            }
            else
            {
                merged.Add(entry);
            }
        }

        return merged;
    }
}
