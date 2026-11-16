using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <param name="Position">The player's chest, about a meter above the feet.</param>
/// <param name="InSight">A clear line from the machine's sensors to the player.</param>
public readonly record struct PlayerTrace(Vector2 Position, Vector2 Velocity, bool InSight);
