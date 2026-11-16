using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// One disagreement: the seam it lies at (<c>Room:Exit | Room:Exit</c>, the upper or western room first; a room's name for its own
/// water), the rule it breaks, and what disagrees in words (positions in the first room's).
/// </summary>
public sealed record SeamProblem(string Seam, SeamRule Rule, string Detail);
