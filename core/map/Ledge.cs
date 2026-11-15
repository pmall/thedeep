namespace TheDeep;

/// <summary>
/// A floor the pilgrim can stand on: the top of a solid at <see cref="Y"/>, feet from <see cref="MinX"/> to <see cref="MaxX"/>;
/// or, with <see cref="Swim"/>, a water's float line (the feet of a body floating on it).
/// </summary>
public readonly record struct Ledge(float Y, float MinX, float MaxX, bool Swim = false);
