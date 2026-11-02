using System;

namespace TheDeep;

/// <summary>
/// The items the pilgrim holds (keys, codes), by id. An item is never spent: once held it opens its doors for good, which keeps the
/// map solver's proof simple (holding only ever helps). Pure logic, unit tested.
/// </summary>
public sealed class Inventory
{
    public ItemSet Held { get; private set; } = ItemSet.Empty;

    /// <summary>Raised with the item's id when it is taken (not when held items are restored).</summary>
    public event Action<string>? Taken;

    public bool Holds(string id) => Held.Contains(id);

    /// <summary>Takes <paramref name="id"/>; false if it was already held.</summary>
    public bool Take(string id)
    {
        if (Held.Contains(id))
        {
            return false;
        }

        Held = Held.With(id);
        Taken?.Invoke(id);
        return true;
    }

    /// <summary>Holds exactly <paramref name="held"/> (a save, a room tried with its route's items).</summary>
    public void Restore(ItemSet held) => Held = held;
}
