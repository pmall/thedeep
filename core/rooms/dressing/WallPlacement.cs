using System.Numerics;

namespace TheDeep;

/// <summary>
/// One piece on a wall: its origin (meters, the wall's frame: X along it from its middle, Y up from its foot, Z out of
/// it toward the camera), its size where the piece is stretched (X, Y), a roll about Z (radians) and, for the pieces that
/// come in several looks (a rack), which <paramref name="Variant"/> to use.
/// </summary>
public readonly record struct WallPlacement(WallPiece Piece, Vector3 Position, Vector2 Size, float Roll = 0f, int Variant = 0);
