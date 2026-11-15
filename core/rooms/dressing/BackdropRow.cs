namespace TheDeep;

/// <summary>
/// A row of backdrop architecture: the pieces' sizes for one depth (<see cref="Scale"/> grows with distance, so far rows
/// read as larger structures), the bay <see cref="Pitch"/>, the span to fill (X from <see cref="MinX"/> to
/// <see cref="MaxX"/>, Y from its foot <see cref="BaseY"/> to its top <see cref="TopY"/>, both off screen) and the band the
/// room's camera bounds span (<see cref="FloorY"/> to <see cref="RoofY"/>), where a style puts what should be seen. Its
/// furnace mouths glow only where <see cref="Glows"/> (the farthest row). Made by <see cref="BackdropRowSpec.Behind"/>.
/// </summary>
public readonly record struct BackdropRow(float MinX, float MaxX, float BaseY, float TopY, float FloorY, float RoofY, float Pitch, float Scale, int Seed, bool Glows = false);
