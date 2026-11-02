namespace TheDeep;

/// <summary>
/// What an item is to the pilgrim: a key (found, it opens a lock) or a code (read, it opens a seal). Its look, its color and its
/// words differ; the map solver treats both the same (an id held for good, <see cref="ItemSet"/>).
/// </summary>
public enum ItemKind
{
    Key,
    Code,
}
