namespace TheDeep;

/// <summary>What a power-up strengthens (docs/roadmap.md#game-systems: one kind of loot, a bonus to strength or defense).</summary>
public enum PowerStat
{
    /// <summary>Each point is one more damage per blow.</summary>
    Strength,

    /// <summary>Each point is one more hit the pilgrim can take (its health's maximum).</summary>
    Defense,
}
