using System.Collections.Generic;
using System.Text;

namespace TheDeep;

/// <summary>
/// Pure: what an item's locked line must name (docs/gameplay.md#gate-says): the item, by its name (a leading "The" aside), and the
/// room it lies in, by the room's name in words (<c>DrownedHall</c> reads "Drowned Hall"), each in any case. The line itself is
/// written by hand in the pilgrim's voice; the room it lies in is the world's data (where its pickup or lever stands), so moving an
/// item without rewriting its line fails the room check.
/// </summary>
public static class LockedLine
{
    /// <summary>What <paramref name="line"/> leaves unnamed of the item <paramref name="itemName"/> lying in <paramref name="room"/>
    /// (a room's id); empty when it names both.</summary>
    public static IReadOnlyList<string> Unnamed(string line, string itemName, string room)
    {
        var missing = new List<string>();
        string name = itemName.Trim();
        if (name.StartsWith("The ", System.StringComparison.OrdinalIgnoreCase))
        {
            name = name[4..];
        }

        foreach (string word in new[] { name, Words(room) })
        {
            if (!line.Contains(word, System.StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(word);
            }
        }

        return missing;
    }

    /// <summary>A room's id in words: a space before each capital after the first letter.</summary>
    public static string Words(string room)
    {
        var words = new StringBuilder(room.Length + 4);
        for (int i = 0; i < room.Length; i++)
        {
            if (i > 0 && char.IsUpper(room[i]) && !char.IsUpper(room[i - 1]))
            {
                words.Append(' ');
            }

            words.Append(room[i]);
        }

        return words.ToString();
    }
}
