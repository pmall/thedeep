
namespace TheDeep;

/// <summary>A place in the game as the solver sees it: a room, and the abilities owned and items held on arriving there.</summary>
public readonly record struct SolverState(string Room, Ability Owned, ItemSet Held)
{
    /// <summary>The place, arrived at holding no item.</summary>
    public SolverState(string room, Ability owned) : this(room, owned, ItemSet.Empty)
    {
    }

    public override string ToString() => $"{Room}{MapSolver.Holding(Owned, Held)}";
}
