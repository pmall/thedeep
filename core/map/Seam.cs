namespace TheDeep;

/// <summary>
/// A meeting of solids <see cref="A"/> and <see cref="B"/> at (<see cref="X"/>, <see cref="Y"/>): a step's height, or the length of a
/// shared face seen (the area seen, square meters, for the face toward the camera).
/// </summary>
public readonly record struct Seam(SeamKind Kind, int A, int B, float X, float Y, float Size);
