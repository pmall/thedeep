namespace TheDeep;

/// <summary>
/// What a world's map is held to (docs/long-term.md, objective 2), from its network file's <c>target</c> statements: at least
/// <see cref="Orders"/> achievable ability orders, every ability obtainable at <see cref="Positions"/> places in the order or
/// more, and between <see cref="MinRooms"/> and <see cref="MaxRooms"/> rooms. A world that names no target is held to none.
/// </summary>
public sealed record MapTargets(int Orders = 0, int Positions = 0, int MinRooms = 0, int MaxRooms = int.MaxValue)
{
    public static MapTargets None { get; } = new();
}
