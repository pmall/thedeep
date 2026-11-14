using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// What the map screen shows of a way out of a visited room (<see cref="MapSheet"/>), at <see cref="At"/>: a point on its room's tile,
/// on the edge it leaves by (<see cref="Side"/>), screen pixels. A taken way is a link across the gap to the room it joins; one not
/// taken yet says whether the pilgrim can take it now, what it lacks for it, or that nothing is built behind it. A hidden way not yet
/// taken (a secret's) has no mark.
/// </summary>
public abstract record WayMark(Vector2 At, ExitSide Side)
{
    /// <summary>A way the pilgrim went through: a link to the room behind it.</summary>
    public sealed record Taken(Vector2 At, ExitSide Side) : WayMark(At, Side);

    /// <summary>A way not taken yet that the pilgrim can take now.</summary>
    public sealed record Open(Vector2 At, ExitSide Side) : WayMark(At, Side);

    /// <summary>A way not taken yet that needs what the pilgrim lacks: the abilities, and the kinds of the items, missing (at least one).</summary>
    public sealed record Gated(Vector2 At, ExitSide Side, IReadOnlyList<Ability> Abilities, IReadOnlyList<ItemKind> Locks) : WayMark(At, Side);

    /// <summary>A way leading where nothing is built yet.</summary>
    public sealed record Unbuilt(Vector2 At, ExitSide Side) : WayMark(At, Side);
}
