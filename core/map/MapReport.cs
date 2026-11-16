using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>What the solver found.</summary>
public sealed class MapReport
{
    /// <summary>States the pilgrim can reach from the start.</summary>
    public int ReachableStates { get; init; }

    /// <summary>Reachable states from which no goal can be reached, each with the path (states) that leads there from the start.</summary>
    public IReadOnlyList<(SolverState State, IReadOnlyList<SolverState> Path)> Softlocks { get; init; } = Array.Empty<(SolverState, IReadOnlyList<SolverState>)>();

    /// <summary>Whether some reachable state reaches a goal.</summary>
    public bool GoalReachable { get; init; }

    /// <summary>The map's rooms no reachable state stands in.</summary>
    public IReadOnlyList<string> UnreachableRooms { get; init; } = Array.Empty<string>();

    /// <summary>The orders in which every ability on the map can be collected.</summary>
    public IReadOnlyList<Ability[]> Orders { get; init; } = Array.Empty<Ability[]>();

    /// <summary>How many abilities lie on the map (orders are permutations of them).</summary>
    public int AbilityCount { get; init; }

    /// <summary>For each ability on the map, the positions (0-based) it takes in some achievable order.</summary>
    public IReadOnlyDictionary<Ability, SortedSet<int>> Positions { get; init; } = new Dictionary<Ability, SortedSet<int>>();

    /// <summary>Whether a goal can be reached, and still can from every reachable state.</summary>
    public bool SoftlockFree => GoalReachable && Softlocks.Count == 0;

    /// <summary>All permutations of the abilities on the map: n!.</summary>
    public int PossibleOrders => Enumerable.Range(1, Math.Max(1, AbilityCount)).Aggregate(1, (f, k) => f * k);
}
