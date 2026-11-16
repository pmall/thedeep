using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>A block a crawler may cling to, as a box on the play plane (room positions); <paramref name="Metal"/> says it can be crawled on.</summary>
public readonly record struct PerchBox(Vector2 Min, Vector2 Max, bool Metal);
