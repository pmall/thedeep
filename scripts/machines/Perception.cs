using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// What a machine can know this tick, gathered by the engine side (MachineDirector) and handed to its brain:
/// its own place, the player (if any), impacts that shook the room, and the machines around it. Positions
/// are on the play plane (X, Y), meters. Senses (<see cref="Senses"/>) turn it into answers like "sees the
/// player" or "heard a landing".
/// </summary>
/// <param name="Time">Seconds since the machine woke into the room.</param>
/// <param name="Position">The machine's origin (its feet, or its center for a flyer).</param>
/// <param name="Facing">+1 facing right, -1 facing left.</param>
/// <param name="Player">The player, or null when there is none in the room.</param>
/// <param name="Impacts">Impacts this tick, anywhere in the room (heard or not).</param>
/// <param name="Neighbors">Other machines nearby.</param>
/// <param name="BlockedLeft">A wall or a drop just to the left: the machine can't go further that way.</param>
/// <param name="BlockedRight">The same to the right.</param>
public readonly record struct Perception(
    float Time,
    Vector2 Position,
    int Facing,
    PlayerTrace? Player,
    IReadOnlyList<Impact> Impacts,
    IReadOnlyList<Neighbor> Neighbors,
    bool BlockedLeft = false,
    bool BlockedRight = false)
{
    /// <summary>Whether the way toward <paramref name="direction"/> (+1 right, -1 left) is blocked.</summary>
    public bool Blocked(int direction) => direction > 0 ? BlockedRight : direction < 0 && BlockedLeft;

    /// <summary>A perception with no player, no impacts and no neighbors.</summary>
    public static Perception Alone(float time, Vector2 position, int facing) =>
        new(time, position, facing, null, Array.Empty<Impact>(), Array.Empty<Neighbor>());
}

/// <param name="Position">The player's chest, about a meter above the feet.</param>
/// <param name="InSight">A clear line from the machine's sensors to the player.</param>
public readonly record struct PlayerTrace(Vector2 Position, Vector2 Velocity, bool InSight);

/// <param name="Strength">How hard it hit, in m/s of impact speed (a hard landing is 15–20).</param>
public readonly record struct Impact(Vector2 Position, float Strength);

/// <param name="Kind">The neighbor's species id.</param>
/// <param name="Signal">What its brain broadcasts (a praying machine's prayer phase).</param>
public readonly record struct Neighbor(Vector2 Position, string Kind, float Signal);
