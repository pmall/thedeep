using System.Collections.Generic;

namespace TheDeep;

/// <summary>A room as the seam check sees it, on its play plane (room positions): its camera bounds, solids, water, lifts and exits.</summary>
public sealed record SeamRoom(Box Bounds, List<Box> Solids, List<(string Name, Box Region)> Waters, List<LiftPath> Lifts, Dictionary<string, Threshold> Exits);
