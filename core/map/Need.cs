namespace TheDeep;

/// <summary>
/// One way a <see cref="Requirement"/> can be met: owning all of <see cref="Abilities"/> and holding all of <see cref="Items"/>.
/// </summary>
public readonly record struct Need(Ability Abilities, ItemSet Items)
{
    public static readonly Need Nothing = new(Ability.None, ItemSet.Empty);

    public bool IsMetBy(Ability owned, ItemSet held) => (owned & Abilities) == Abilities && Items.IsSubsetOf(held);

    /// <summary>Whether meeting <paramref name="other"/> always meets this one too (this one needs no more).</summary>
    public bool IsCoveredBy(Need other) => (other.Abilities & Abilities) == Abilities && Items.IsSubsetOf(other.Items);

    /// <summary>Both at once.</summary>
    public Need And(Need other) => new(Abilities | other.Abilities, Items.Union(other.Items));
}
