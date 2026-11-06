using System.Numerics;

namespace TheDeep;

/// <summary>
/// How rooms sit on the map, pure: positions are room-local (X, Y) on the play plane, an exit's position is its marker. How an exit
/// is crossed is <see cref="Threshold"/>'s.
/// </summary>
public static class ExitMath
{
    /// <summary>
    /// Where a room's origin sits on the map, given the room it was entered from: the departure marker
    /// and the arrival marker are the same place in the world.
    /// </summary>
    public static Vector2 RoomOffset(Vector2 fromRoomOffset, Vector2 fromExit, Vector2 toExit) => fromRoomOffset + fromExit - toExit;
}
