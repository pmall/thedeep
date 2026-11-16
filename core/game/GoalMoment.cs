
namespace TheDeep;

/// <summary>What reaching a goal does (<see cref="Pilgrimage.ReachGoal"/>).</summary>
public enum GoalMoment
{
    None,

    /// <summary>The end of what is built: its banner is shown.</summary>
    Shown,

    /// <summary>The end of the game: the end card, then a new game.</summary>
    End,
}
