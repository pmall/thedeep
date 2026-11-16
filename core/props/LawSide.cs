using System;

namespace TheDeep;

/// <summary>Which side of a law the forbidden way lies on; the values are the direction along X (east +1, west −1).</summary>
public enum LawSide
{
    /// <summary>The forbidden side lies to the west.</summary>
    West = -1,

    /// <summary>The forbidden side lies to the east.</summary>
    East = 1,
}
