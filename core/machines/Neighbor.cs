using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <param name="Kind">The neighbor's species id.</param>
/// <param name="Signal">What its brain broadcasts (a praying machine's prayer phase).</param>
public readonly record struct Neighbor(Vector2 Position, string Kind, float Signal);
