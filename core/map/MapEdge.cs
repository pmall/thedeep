using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>A way from one room to another: leaving <see cref="From"/> through its exit, needing <see cref="Requires"/>.</summary>
public sealed record MapEdge(string From, string FromExit, string To, string ToExit, Requirement Requires);
