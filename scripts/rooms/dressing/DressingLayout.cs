using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>A slot in a row of dressing pieces: its index along the row, the variant to use, and whether it lies toppled.</summary>
public readonly record struct DressingSlot(int Index, int Variant, bool Toppled);

/// <summary>
/// Pure, deterministic layouts for the dressing generators (<see cref="Colonnade"/>, <see cref="RackRow"/>):
/// which bays carry cables, which slots of a row stay empty, which pieces lie toppled. The same seed always
/// gives the same room, so captures stay comparable.
/// </summary>
public static class DressingLayout
{
    /// <summary>A value in [0, 1) from a seed and an index (an integer hash, stable across runs and platforms).</summary>
    public static float Hash01(int seed, int index)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)index * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>The bays (0 to <paramref name="bays"/> − 1) that get something, each with probability <paramref name="chance"/>.</summary>
    public static IEnumerable<int> Chosen(int bays, float chance, int seed)
    {
        for (int i = 0; i < bays; i++)
        {
            if (Hash01(seed, i) < chance)
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// The filled slots of a row of <paramref name="count"/>: a slot stays empty with probability
    /// <paramref name="gapChance"/>, a filled one lies toppled with probability <paramref name="toppleChance"/>
    /// (never two toppled side by side), and takes one of <paramref name="variants"/> variants, never the same
    /// as its left neighbor when there are several.
    /// </summary>
    public static List<DressingSlot> Row(int count, int variants, float gapChance, float toppleChance, int seed)
    {
        var slots = new List<DressingSlot>();
        int previousVariant = -1;
        bool previousToppled = false;
        for (int i = 0; i < count; i++)
        {
            if (Hash01(seed, 3 * i) < gapChance)
            {
                previousVariant = -1;
                previousToppled = false;
                continue;
            }

            int variant = variants <= 1 ? 0 : (int)(Hash01(seed, 3 * i + 1) * variants);
            if (variants > 1 && variant == previousVariant)
            {
                variant = (variant + 1) % variants;
            }

            bool toppled = !previousToppled && Hash01(seed, 3 * i + 2) < toppleChance;
            slots.Add(new DressingSlot(i, Math.Clamp(variant, 0, Math.Max(0, variants - 1)), toppled));
            previousVariant = variant;
            previousToppled = toppled;
        }

        return slots;
    }

    /// <summary>Whether <paramref name="x"/> lies within <paramref name="radius"/> of some <paramref name="offset"/> + k × <paramref name="every"/>.</summary>
    public static bool NearRepeat(float x, float offset, float every, float radius)
    {
        float d = (x - offset) % every;
        if (d < 0f)
        {
            d += every;
        }

        return Math.Min(d, every - d) <= radius + 1e-4f;
    }
}
