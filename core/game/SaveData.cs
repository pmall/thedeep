using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheDeep;

/// <summary>A visited room on the saved map.</summary>
public sealed record SavedRoom(string Name, float X, float Y, float MinX, float MinY, float MaxX, float MaxY);

/// <summary>A way out of a visited room on the saved map (<see cref="MapWay"/>).</summary>
public sealed record SavedWay(string Room, string Exit, float X, float Y, string Requires, bool Built);

/// <summary>
/// What a save holds, pure: the shrine the pilgrim last rested at (where it comes back after dying or
/// relaunching), its abilities, items and prowess, what is done for good (relics taken, doors opened...), and the map of visited
/// rooms. Stored as JSON in
/// user://save.json by <see cref="SaveFile"/>.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>The room of the last shrine rested at (a room name, its scene's file name).</summary>
    public string Room { get; set; } = "";

    /// <summary>The shrine's node name in that room; empty for the room's spawn point.</summary>
    public string Shrine { get; set; } = "";

    /// <summary>The abilities owned (<see cref="Ability"/> flags).</summary>
    public int Abilities { get; set; }

    /// <summary>What the power-ups taken add (<see cref="Prowess"/>): damage per blow, and health.</summary>
    public int Strength { get; set; }

    public int Defense { get; set; }

    /// <summary>The items held (keys, codes), by id (empty in a save from before items).</summary>
    public List<string> Items { get; set; } = new();

    /// <summary>What is done once and for good, as "Room/NodeName": relics and items taken, bosses beaten, laws broken, doors opened.</summary>
    public List<string> Collected { get; set; } = new();

    public List<SavedRoom> Map { get; set; } = new();

    /// <summary>The visited rooms' ways out (empty in a save from before they were kept).</summary>
    public List<SavedWay> Ways { get; set; } = new();

    /// <summary>The ways taken, as "Room/Exit".</summary>
    public List<string> Taken { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads a save; null if the text isn't one (or is from a newer version).</summary>
    public static SaveData? FromJson(string json)
    {
        try
        {
            SaveData? data = JsonSerializer.Deserialize<SaveData>(json, Options);
            return data is null || data.Version > CurrentVersion || data.Room == "" ? null : data;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The map as the game keeps it.</summary>
    public IEnumerable<MapRoom> MapRooms()
    {
        foreach (SavedRoom r in Map)
        {
            yield return new MapRoom(r.Name, new System.Numerics.Vector2(r.X, r.Y), new System.Numerics.Vector2(r.MinX, r.MinY),
                new System.Numerics.Vector2(r.MaxX, r.MaxY));
        }
    }

    public void SetMap(IEnumerable<MapRoom> rooms)
    {
        Map.Clear();
        foreach (MapRoom r in rooms)
        {
            Map.Add(new SavedRoom(r.Name, r.Offset.X, r.Offset.Y, r.BoundsMin.X, r.BoundsMin.Y, r.BoundsMax.X, r.BoundsMax.Y));
        }
    }

    /// <summary>Keeps the whole map: rooms, their ways out, the ways taken.</summary>
    public void SetMap(MapState map)
    {
        SetMap(map.Rooms);
        Ways.Clear();
        foreach (MapWay w in map.Ways)
        {
            Ways.Add(new SavedWay(w.Room, w.Exit, w.Position.X, w.Position.Y, w.Requires, w.Built));
        }

        Taken = new List<string>(map.TakenWays);
        Taken.Sort(System.StringComparer.Ordinal);
    }

    /// <summary>The saved ways out as the map keeps them.</summary>
    public IEnumerable<MapWay> MapWays()
    {
        foreach (SavedWay w in Ways)
        {
            yield return new MapWay(w.Room, w.Exit, new System.Numerics.Vector2(w.X, w.Y), w.Requires, w.Built);
        }
    }

    /// <summary>The id a relic (or a beaten boss's seal, a broken law) is remembered by.</summary>
    public static string RelicId(string room, string node) => $"{room}/{node}";
}
