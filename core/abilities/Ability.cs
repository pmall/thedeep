using System;

namespace TheDeep;

/// <summary>The unlockable abilities. Each gates part of the map.</summary>
[Flags]
public enum Ability
{
    None = 0,
    MagGrip = 1 << 0,
    Harpoon = 1 << 1,
    Drill = 1 << 2,
    Ballast = 1 << 3,
    HeatPlating = 1 << 4,
}
