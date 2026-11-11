using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// Items held or needed (keys, codes: <see cref="ItemKind"/>), by id, as a value: two sets holding the same ids are equal, so a set
/// can be part of a solver state. Immutable, sorted.
/// </summary>
public sealed class ItemSet : IEquatable<ItemSet>
{
    /// <summary>The set of no item: what a pilgrim holds at the start, what a way with no lock needs.</summary>
    public static readonly ItemSet Empty = new(Array.Empty<string>());

    private readonly string[] _ids;
    private readonly int _hash;

    private ItemSet(string[] sortedIds)
    {
        _ids = sortedIds;
        var hash = new HashCode();
        foreach (string id in _ids)
        {
            hash.Add(id, StringComparer.Ordinal);
        }

        _hash = hash.ToHashCode();
    }

    /// <summary>The set of <paramref name="ids"/>; one named twice counts once.</summary>
    public static ItemSet Of(IEnumerable<string> ids)
    {
        string[] sorted = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return sorted.Length == 0 ? Empty : new ItemSet(sorted);
    }

    /// <summary>The set of the given ids; one named twice counts once.</summary>
    public static ItemSet Of(params string[] ids) => Of((IEnumerable<string>)ids);

    /// <summary>The ids, sorted.</summary>
    public IReadOnlyList<string> Ids => _ids;

    /// <summary>How many items are in the set.</summary>
    public int Count => _ids.Length;

    /// <summary>Whether the item <paramref name="id"/> is in the set.</summary>
    public bool Contains(string id) => Array.BinarySearch(_ids, id, StringComparer.Ordinal) >= 0;

    /// <summary>Whether every id here is in <paramref name="other"/>.</summary>
    public bool IsSubsetOf(ItemSet other) => _ids.Length <= other._ids.Length && _ids.All(other.Contains);

    /// <summary>The set of every item in this one or in <paramref name="other"/>.</summary>
    public ItemSet Union(ItemSet other) =>
        other._ids.Length == 0 || ReferenceEquals(this, other) ? this : _ids.Length == 0 ? other : Of(_ids.Concat(other._ids));

    /// <summary>This set with the item <paramref name="id"/> added; itself if it is already there.</summary>
    public ItemSet With(string id) => Contains(id) ? this : Of(_ids.Append(id));

    /// <summary>Whether both sets hold exactly the same ids.</summary>
    public bool Equals(ItemSet? other) => other is not null && _hash == other._hash && _ids.AsSpan().SequenceEqual(other._ids);

    public override bool Equals(object? obj) => obj is ItemSet other && Equals(other);

    public override int GetHashCode() => _hash;

    /// <summary>The ids joined by commas; empty for none.</summary>
    public override string ToString() => string.Join(",", _ids);
}
