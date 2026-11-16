using System.Numerics;

namespace TheDeep;

/// <summary>A row of framing silhouettes: how many, and the area (X, Y) their bases are scattered in.</summary>
public readonly record struct FrameBand(int Count, Vector2 AreaMin, Vector2 AreaMax);
