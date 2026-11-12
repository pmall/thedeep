using Godot;

namespace TheDeep;

/// <summary>
/// One item of a world (a key, a code): its id, which exits' requirements name (<see cref="Requirement"/>: <c>CryptKey</c>,
/// <c>Drill &amp; CryptKey</c>), what kind it is, and its lines. A world lists its items (<see cref="WorldDefinition.Items"/>); an
/// <see cref="ItemPickup"/> lays one in a room and a <see cref="LockedDoor"/> opens for it.
/// </summary>
[GlobalClass]
public partial class ItemDefinition : Resource, INamesLines
{
    /// <summary>The id requirements name: a word starting with a letter, not an ability's name (the room check holds it to that).</summary>
    [Export] public string Id { get; set; } = "";

    [Export] public ItemKind Kind { get; set; } = ItemKind.Key;

    /// <summary>The item's name (shown when taken and on the map).</summary>
    [Export] public StringName NameLine { get; set; } = "";

    /// <summary>Shown under the name when it is taken: what it is, where it might fit.</summary>
    [Export] public StringName TakenLine { get; set; } = "";

    /// <summary>What the pilgrim says seeing its door without it: it names the item and the room it lies in (<see cref="TheDeep.LockedLine"/>).</summary>
    [Export] public StringName LockedLine { get; set; } = "";

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds => new[] { NameLine, TakenLine, LockedLine };
}
