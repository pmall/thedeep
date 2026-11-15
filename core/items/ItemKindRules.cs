using System;

namespace TheDeep;

/// <summary>What each kind of item is to the pilgrim's rules (<see cref="ItemKind"/>).</summary>
public static class ItemKindRules
{
    /// <summary>Whether the pilgrim carries an item of <paramref name="kind"/> (a key, a code), so the map lists it; a mechanism's state stays in the world.</summary>
    public static bool Carried(this ItemKind kind) => kind switch
    {
        ItemKind.Key => true,
        ItemKind.Code => true,
        ItemKind.Mechanism => false,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
