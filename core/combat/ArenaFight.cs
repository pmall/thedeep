namespace TheDeep;

/// <summary>
/// The rule of a sealed arena (a boss's, an ambush's), pure: it waits until the pilgrim, in control, steps inside while foes stand;
/// then the fight is on until none stands; then it is won for good.
/// </summary>
public sealed class ArenaFight
{
    /// <summary>Where the fight stands; waiting until the pilgrim steps in.</summary>
    public ArenaPhase Phase { get; private set; }

    /// <summary>Won before (from the save).</summary>
    public void Settle() => Phase = ArenaPhase.Won;

    /// <param name="pilgrimIn">The pilgrim, in control, has its feet inside the arena.</param>
    /// <param name="foesStand">Some foe of the arena stands (or waits to spring).</param>
    public ArenaChange Step(bool pilgrimIn, bool foesStand)
    {
        switch (Phase)
        {
            case ArenaPhase.Waiting when pilgrimIn && foesStand:
                Phase = ArenaPhase.Fighting;
                return ArenaChange.Began;
            case ArenaPhase.Fighting when !foesStand:
                Phase = ArenaPhase.Won;
                return ArenaChange.Cleared;
            default:
                return ArenaChange.None;
        }
    }
}
