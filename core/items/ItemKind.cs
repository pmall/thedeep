namespace TheDeep;

/// <summary>
/// What an item is to the pilgrim: a key (found, it opens a lock), a code (read, it opens a seal), or a mechanism's state (a lever
/// pulled: its gate opens at once, wherever the pilgrim stands; the world holds it, so the pilgrim's list does not show it). Its
/// look, its color and its words differ; the map solver treats them all the same (an id held for good, <see cref="ItemSet"/>).
/// </summary>
public enum ItemKind
{
    Key,
    Code,
    Mechanism,
}
