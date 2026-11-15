using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Names made from a key on first asking and kept for the process, so a name built from text (a line id from an
/// ability) is never left to the finalizer (instructions/godot-csharp.md#no-finalizer). Held in a <c>static readonly</c>
/// field; main thread only.
/// </summary>
public sealed class NameCache<TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, StringName> _names = new();
    private readonly Func<TKey, string> _text;

    /// <summary>A cache naming each key <paramref name="text"/>(key).</summary>
    public NameCache(Func<TKey, string> text) => _text = text;

    /// <summary>The name of <paramref name="key"/>, made once.</summary>
    public StringName this[TKey key]
    {
        get
        {
            if (!_names.TryGetValue(key, out StringName? name))
            {
#pragma warning disable DEEP001 // kept in the cache for the process: never finalized
                name = new StringName(_text(key));
#pragma warning restore DEEP001
                _names[key] = name;
            }

            return name;
        }
    }
}
