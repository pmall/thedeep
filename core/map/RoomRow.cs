using System.Collections.Generic;

namespace TheDeep;

/// <summary>One row of a <see cref="RoomTable"/>.</summary>
public sealed record RoomRow(string Room, string Layer, IReadOnlyList<RoomKind> Kinds, string Happens, IReadOnlyList<string> Machines,
    IReadOnlyList<string> Items, string Notes);
