using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Where the player arrives when crossing between rooms, and how rooms sit relative to each other.
/// Pure: positions are room-local (X, Y) on the play plane, an exit's position is its marker (where the
/// player stands on arrival) and its size is its trigger area.
/// </summary>
public static class ExitMath
{
    /// <summary>Keeps arrivals this far inside the edge of the arrival exit's area, meters.</summary>
    public const float EdgeMargin = 0.3f;

    /// <summary>A wide exit (a hatch, a drop) is crossed vertically; a tall one (a doorway) horizontally.</summary>
    public static bool IsVertical(Vector2 size) => size.X > size.Y;

    /// <summary>
    /// The player's arrival position in the target room: the arrival marker, shifted by the player's
    /// offset from the departure marker along the exit's long axis (so crossing a wide hatch off-center
    /// arrives off-center), clamped inside the arrival area. Across the exit the offset is dropped, and
    /// a doorway never puts the feet below its marker.
    /// </summary>
    public static Vector2 Arrival(Vector2 player, Vector2 from, Vector2 to, Vector2 toSize)
    {
        Vector2 offset = player - from;
        if (IsVertical(toSize))
        {
            float half = Math.Max(0f, toSize.X * 0.5f - EdgeMargin);
            return to + new Vector2(Math.Clamp(offset.X, -half, half), 0f);
        }

        float up = Math.Max(0f, toSize.Y * 0.5f - EdgeMargin);
        return to + new Vector2(0f, Math.Clamp(offset.Y, 0f, up));
    }

    /// <summary>
    /// The velocity to keep on arrival: unchanged, except that climbing up through a hatch always keeps at
    /// least <paramref name="minRise"/> upward so the player clears the opening.
    /// </summary>
    public static Vector2 ArrivalVelocity(Vector2 velocity, Vector2 toSize, float minRise)
    {
        if (IsVertical(toSize) && velocity.Y > 0f)
        {
            return velocity with { Y = Math.Max(velocity.Y, minRise) };
        }

        return velocity;
    }

    /// <summary>
    /// Where a room's origin sits on the map, given the room it was entered from: the departure marker
    /// and the arrival marker are the same place in the world.
    /// </summary>
    public static Vector2 RoomOffset(Vector2 fromRoomOffset, Vector2 fromExit, Vector2 toExit) => fromRoomOffset + fromExit - toExit;
}
