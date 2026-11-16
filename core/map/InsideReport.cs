using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>What the inside solver found.</summary>
public sealed class InsideReport
{
    /// <summary>How many states can be reached from the start.</summary>
    public int ReachableStates { get; init; }

    /// <summary>Whether some reachable state reaches a goal.</summary>
    public bool GoalReachable { get; init; }

    /// <summary>Reachable states from which no goal can be reached, each with the path (states) that leads there from the start.</summary>
    public IReadOnlyList<(WayState State, IReadOnlyList<WayState> Path)> Softlocks { get; init; } = Array.Empty<(WayState, IReadOnlyList<WayState>)>();

    /// <summary>Rooms no reachable state is in.</summary>
    public IReadOnlyList<string> UnenteredRooms { get; init; } = Array.Empty<string>();

    /// <summary>Relics no reachable state takes.</summary>
    public IReadOnlyList<MapRelic> UntakenRelics { get; init; } = Array.Empty<MapRelic>();

    /// <summary>Items no reachable state takes.</summary>
    public IReadOnlyList<MapItem> UntakenItems { get; init; } = Array.Empty<MapItem>();

    /// <summary>Whether a goal can be reached, and still can from every reachable state.</summary>
    public bool SoftlockFree => GoalReachable && Softlocks.Count == 0;
}
