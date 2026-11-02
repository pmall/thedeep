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
/// A way out of a visited room as the map knows it: the exit's name and where it sits in its room, what it needs
/// (a <see cref="Requirement"/> expression), whether the room behind it is built, and whether it is hidden (a secret's way in: the map
/// shows it only once taken).
/// </summary>
public sealed record MapWay(string Room, string Exit, Vector2 Position, string Requires, bool Built, bool Hidden = false);

/// <summary>
/// The rooms the player has visited and where they sit relative to each other. Nothing is listed by hand:
/// a room's place comes from the exit the player entered it through (<see cref="ExitMath.RoomOffset"/>),
/// the first room sits at the origin. Pure, saved with the game.
/// </summary>
public sealed class MapState
{
    private readonly Dictionary<string, MapRoom> _rooms = new();
    private readonly Dictionary<string, List<MapWay>> _ways = new();
    private readonly HashSet<string> _taken = new();

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

    /// <summary>Every way out the map knows of, in the visited rooms.</summary>
    public IEnumerable<MapWay> Ways
    {
        get
        {
            foreach (List<MapWay> ways in _ways.Values)
            {
                foreach (MapWay way in ways)
                {
                    yield return way;
                }
            }
        }
    }

    /// <summary>The ways taken so far, as "Room/Exit": left through, or arrived through.</summary>
    public IReadOnlyCollection<string> TakenWays => _taken;

    /// <summary>Records a visited room's ways out (from its exit markers), replacing what was known of them.</summary>
    public void SetWays(string room, IEnumerable<MapWay> ways) => _ways[room] = new List<MapWay>(ways);

    public IReadOnlyList<MapWay> WaysOf(string room) => _ways.TryGetValue(room, out List<MapWay>? ways) ? ways : System.Array.Empty<MapWay>();

    /// <summary>The pilgrim went through this exit (leaving or arriving): the way is taken.</summary>
    public void Take(string room, string exit) => _taken.Add(WayId(room, exit));

    public bool Taken(string room, string exit) => _taken.Contains(WayId(room, exit));

    public static string WayId(string room, string exit) => $"{room}/{exit}";

    /// <summary>Restores a saved map.</summary>
    public void Restore(IEnumerable<MapRoom> rooms, string current, IEnumerable<MapWay>? ways = null, IEnumerable<string>? taken = null)
    {
        _rooms.Clear();
        _ways.Clear();
        _taken.Clear();
        foreach (MapRoom room in rooms)
        {
            _rooms[room.Name] = room;
        }

        foreach (MapWay way in ways ?? System.Array.Empty<MapWay>())
        {
            if (!_ways.TryGetValue(way.Room, out List<MapWay>? list))
            {
                _ways[way.Room] = list = new List<MapWay>();
            }

            list.Add(way);
        }

        _taken.UnionWith(taken ?? System.Array.Empty<string>());
        Current = current;
    }
}
