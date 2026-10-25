using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>A room as the map knows it: where its origin sits on the map and the area it covers (its camera bounds).</summary>
public sealed record MapRoom(string Name, Vector2 Offset, Vector2 BoundsMin, Vector2 BoundsMax)
{
    public Vector2 MapMin => Offset + BoundsMin;
    public Vector2 MapMax => Offset + BoundsMax;
}

/// <summary>
/// The rooms the player has visited and where they sit relative to each other. Nothing is listed by hand:
/// a room's place comes from the exit the player entered it through (<see cref="ExitMath.RoomOffset"/>),
/// the first room sits at the origin. Pure, saved with the game.
/// </summary>
public sealed class MapState
{
    private readonly Dictionary<string, MapRoom> _rooms = new();

    public IReadOnlyCollection<MapRoom> Rooms => _rooms.Values;

    public string Current { get; private set; } = "";

    public bool Knows(string room) => _rooms.ContainsKey(room);

    public MapRoom? Find(string room) => _rooms.TryGetValue(room, out MapRoom? found) ? found : null;

    /// <summary>
    /// Records entering <paramref name="room"/>. A room seen before keeps its first place, so the map
    /// never shifts under the player; a new one is placed at <paramref name="offset"/>.
    /// </summary>
    public MapRoom Enter(string room, Vector2 offset, Vector2 boundsMin, Vector2 boundsMax)
    {
        Current = room;
        if (!_rooms.TryGetValue(room, out MapRoom? known))
        {
            known = new MapRoom(room, offset, boundsMin, boundsMax);
            _rooms[room] = known;
        }

        return known;
    }

    /// <summary>Restores a saved map.</summary>
    public void Restore(IEnumerable<MapRoom> rooms, string current)
    {
        _rooms.Clear();
        foreach (MapRoom room in rooms)
        {
            _rooms[room.Name] = room;
        }

        Current = current;
    }
}
