
namespace TheDeep;

/// <summary>
/// A place in the game as the inside solver sees it: a room, the way the pilgrim came in, and the abilities owned and items held on
/// arriving.
/// </summary>
public readonly record struct WayState(string Room, WayEntry Entry, Ability Owned, ItemSet Held)
{
    /// <summary>The place, arrived at holding no item.</summary>
    public WayState(string room, WayEntry entry, Ability owned) : this(room, entry, owned, ItemSet.Empty)
    {
    }

    public override string ToString() => $"{Room} via {Entry}{MapSolver.Holding(Owned, Held)}";
}
