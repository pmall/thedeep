using System.Numerics;

namespace TheDeep;

/// <summary>A thin box just off a face, to be tested for anything else touching that face: its center and its size on the play plane.</summary>
public readonly record struct FaceProbe(Vector2 Center, Vector2 Size);
