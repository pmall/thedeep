using System;
using Godot;

namespace TheDeep;

/// <summary>
/// Typed engine arrays released deterministically (instructions/godot-csharp.md#no-finalizer). An
/// <c>Array&lt;T&gt;</c> is not <see cref="IDisposable"/> in Godot 4.7: the untyped array under it is, and is what the
/// finalizer would release. Each method here takes the array (<see cref="OwnsAttribute"/>) and releases it.
/// </summary>
public static class EngineArrays
{
    /// <summary>How many items <paramref name="array"/> holds; the array is released.</summary>
    public static int Count<[MustBeVariant] T>([Owns] Godot.Collections.Array<T> array)
    {
        using var owner = (Godot.Collections.Array)array;
        return array.Count;
    }

    /// <summary>The items of <paramref name="array"/>, in order; the array is released.</summary>
    public static T[] Take<[MustBeVariant] T>([Owns] Godot.Collections.Array<T> array)
    {
        using var owner = (Godot.Collections.Array)array;
        var items = new T[array.Count];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = array[i];
        }

        return items;
    }

    /// <summary><paramref name="array"/>, to be released by a <c>using</c> once the engine call that reads it is made.</summary>
    public static Owned<T> Own<[MustBeVariant] T>([Owns] Godot.Collections.Array<T> array) => new(array);

    /// <summary>A typed array whose <see cref="Dispose"/> releases it.</summary>
    public readonly struct Owned<[MustBeVariant] T> : IDisposable
    {
        internal Owned(Godot.Collections.Array<T> value) => Value = value;

        /// <summary>The array, valid until disposed.</summary>
        public Godot.Collections.Array<T> Value { get; }

        public void Dispose() => ((Godot.Collections.Array)Value).Dispose();
    }
}
