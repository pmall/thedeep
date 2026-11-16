using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>A room as the map knows it: where its origin sits on the map and the area it covers (its camera bounds).</summary>
public sealed record MapRoom(string Name, Vector2 Offset, Vector2 BoundsMin, Vector2 BoundsMax)
{
    /// <summary>The minimum corner of the area the room covers, on the map (meters).</summary>
    public Vector2 MapMin => Offset + BoundsMin;
    /// <summary>The maximum corner of the area the room covers, on the map (meters).</summary>
    public Vector2 MapMax => Offset + BoundsMax;
}
