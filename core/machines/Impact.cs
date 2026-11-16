using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <param name="Strength">How hard it hit, in m/s of impact speed (a hard landing is 15–20).</param>
public readonly record struct Impact(Vector2 Position, float Strength);
