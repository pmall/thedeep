using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheDeep;

/// <summary>A visited room on the saved map.</summary>
public sealed record SavedRoom(string Name, float X, float Y, float MinX, float MinY, float MaxX, float MaxY);

/// <summary>A way out of a visited room on the saved map (<see cref="MapWay"/>).</summary>
public sealed record SavedWay(string Room, string Exit, float X, float Y, string Requires, bool Built, bool Hidden = false);

/// <summary>What reading a save's text gives: the save, or why it cannot be one (<see cref="SaveData.Parse"/>).</summary>
public abstract record SaveReading
{
    /// <summary>A save this build reads.</summary>
    public sealed record Loaded(SaveData Data) : SaveReading;

    /// <summary>A save written by a newer build (<paramref name="Found"/> is its format, <paramref name="Known"/> the newest this build reads).</summary>
    public sealed record Newer(int Found, int Known) : SaveReading;

    /// <summary>Text that is not a save, and what is wrong with it.</summary>
    public sealed record Corrupt(string Why) : SaveReading;

    /// <summary>What is wrong with a save that cannot be read, as one line for the log; empty for a loaded one.</summary>
    public string Problem => this switch
    {
        Newer n => $"it was written by a newer version of the game (save format {n.Found}, this build reads up to {n.Known})",
        Corrupt c => $"it is corrupt ({c.Why})",
        _ => "",
    };
}

/// <summary>
/// What a save holds, pure: the shrine the pilgrim last rested at (where it comes back after dying or
/// relaunching), its abilities, items and prowess, what is done for good (relics taken, doors opened...), and the map of visited
/// rooms. Stored as JSON in
/// user://save.json by <see cref="SaveFile"/>.
/// </summary>
public sealed class SaveData
{
    /// <summary>The save format this build writes; a save with a higher version is refused.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The save format this save was written in.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>The room of the last shrine rested at (a room name, its scene's file name).</summary>
    public string Room { get; set; } = "";

    /// <summary>The shrine's node name in that room; empty for the room's spawn point.</summary>
    public string Shrine { get; set; } = "";

    /// <summary>The abilities owned (<see cref="Ability"/> flags).</summary>
    public int Abilities { get; set; }

    /// <summary>What the power-ups taken add (<see cref="Prowess"/>): damage per blow, and health.</summary>
    public int Strength { get; set; }

    /// <summary>Health added by the power-ups taken (<see cref="Prowess"/>).</summary>
    public int Defense { get; set; }

    /// <summary>The items held (keys, codes), by id (empty in a save from before items).</summary>
    public List<string> Items { get; set; } = new();

    /// <summary>What is done once and for good, as "Room/NodeName": relics and items taken, bosses beaten, laws broken, doors opened.</summary>
    public List<string> Collected { get; set; } = new();

    /// <summary>The rooms on the pilgrim's map, each with where it sits and its bounds.</summary>
    public List<SavedRoom> Map { get; set; } = new();

    /// <summary>The visited rooms' ways out (empty in a save from before they were kept).</summary>
    public List<SavedWay> Ways { get; set; } = new();

    /// <summary>The ways taken, as "Room/Exit".</summary>
    public List<string> Taken { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    /// <summary>Where a save is first written, beside <paramref name="path"/>, before it is renamed over it (a crash mid-write leaves the old save whole).</summary>
    public static string TempPath(string path) => path + ".tmp";

    /// <summary>Where a save that cannot be read is kept, beside <paramref name="path"/>, so the new game that follows never overwrites it.</summary>
    public static string AsidePath(string path) => path + ".bad";

    /// <summary>The save as indented JSON text, as written to the file.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads a save; null if the text isn't one (or is from a newer version): <see cref="Parse"/> says why.</summary>
    public static SaveData? FromJson(string json) => Parse(json) is SaveReading.Loaded loaded ? loaded.Data : null;

    /// <summary>Reads a save, or says why the text cannot be one: a newer version's, or corrupt.</summary>
    public static SaveReading Parse(string json)
    {
        try
        {
            SaveData? data = JsonSerializer.Deserialize<SaveData>(json, Options);
            return data switch
            {
                null => new SaveReading.Corrupt("it holds no save"),
                { Version: > CurrentVersion } => Newer(data.Version),
                { Room: "" } => new SaveReading.Corrupt("it names no room"),
                _ => new SaveReading.Loaded(data),
            };
        }
        catch (JsonException e)
        {
            // A newer format can differ in shape, so its version is looked for before calling the text corrupt.
            return VersionOf(json) is { } version and > CurrentVersion ? Newer(version) : new SaveReading.Corrupt(e.Message);
        }
    }

    private static SaveReading.Newer Newer(int version) => new(version, CurrentVersion);

    private static int? VersionOf(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(nameof(Version), out JsonElement element)
                && element.TryGetInt32(out int version)
                ? version
                : null;
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

    /// <summary>Replaces the saved rooms with <paramref name="rooms"/>.</summary>
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
            Ways.Add(new SavedWay(w.Room, w.Exit, w.Position.X, w.Position.Y, w.Requires, w.Built, w.Hidden));
        }

        Taken = new List<string>(map.TakenWays);
        Taken.Sort(System.StringComparer.Ordinal);
    }

    /// <summary>The saved ways out as the map keeps them.</summary>
    public IEnumerable<MapWay> MapWays()
    {
        foreach (SavedWay w in Ways)
        {
            yield return new MapWay(w.Room, w.Exit, new System.Numerics.Vector2(w.X, w.Y), w.Requires, w.Built, w.Hidden);
        }
    }

    /// <summary>The id a relic (or a beaten boss's seal, a broken law) is remembered by.</summary>
    public static string RelicId(string room, string node) => $"{room}/{node}";
}
