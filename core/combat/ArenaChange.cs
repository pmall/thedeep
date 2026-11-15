namespace TheDeep;

/// <summary>What a step of an arena's fight did (<see cref="ArenaFight.Step"/>).</summary>
public enum ArenaChange
{
    None,

    /// <summary>The pilgrim stepped in while foes stand: the gates fall.</summary>
    Began,

    /// <summary>The last foe broke: the gates rise, for good.</summary>
    Cleared,
}
