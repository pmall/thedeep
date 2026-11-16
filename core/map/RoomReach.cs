using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// What a pilgrim can do inside a room, come in through <see cref="WayState.Entry"/> with <see cref="WayState.Owned"/>: the abilities
/// it owns and the items it holds once it has taken the relics and items it can reach there, the exits it can reach (their
/// requirements still apply), and whether it reaches a goal. The geometry planner answers it for the built rooms; tests by hand.
/// </summary>
public readonly record struct RoomReach(Ability Owned, ItemSet Held, IReadOnlyCollection<string> Exits, bool Goal)
{
    /// <summary>What is reached holding no item.</summary>
    public RoomReach(Ability owned, IReadOnlyCollection<string> exits, bool goal) : this(owned, ItemSet.Empty, exits, goal)
    {
    }
}
