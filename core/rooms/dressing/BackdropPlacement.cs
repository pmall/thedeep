using System.Numerics;

namespace TheDeep;

/// <summary>
/// One piece placed in a backdrop row: where its origin goes (meters, the row's frame: X along the room, Y up, Z toward
/// the camera), its rotation about Z then X (radians), and its scale.
/// </summary>
public readonly record struct BackdropPlacement(BackdropPiece Piece, Vector3 Position, float RollZ = 0f, float PitchX = 0f, float Length = 1f);
