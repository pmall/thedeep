using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// A world as data (instructions/principles.md#bricks): its rooms, its network, its layers and their biomes, its items, its line book
/// and where it starts. The code runs whatever world it is given: the prototype is one (worlds/prototype/world.tres), the real
/// game will be another. Picked with `--world &lt;name&gt;` (worlds/&lt;name&gt;/world.tres), else the main scene's.
/// </summary>
[GlobalClass]
public partial class WorldDefinition : Resource
{
    /// <summary>Where worlds live: one folder each, its definition in <see cref="FileName"/>.</summary>
    public const string Folder = "res://worlds";

    /// <summary>The name of a world's definition file in its folder.</summary>
    public const string FileName = "world.tres";

    /// <summary>Folder of the world's room scenes; a room's name is its file name there.</summary>
    [Export(PropertyHint.Dir)] public string RoomsFolder { get; set; } = "";

    /// <summary>
    /// The world's network: a markdown file whose `map` blocks are its planning graph (<see cref="MapDraft"/>), checked against
    /// the built rooms on every build.
    /// </summary>
    [Export(PropertyHint.File, "*.md")] public string Network { get; set; } = "";

    /// <summary>The world's room table, beside its network: one row per room (<see cref="TheDeep.RoomTable"/>), held to the network on every build.</summary>
    [Export(PropertyHint.File, "*.tsv")] public string RoomTable { get; set; } = "";

    /// <summary>Where a new game starts.</summary>
    [Export] public PackedScene StartRoom { get; set; } = null!;

    /// <summary>The descent's layers, from the surface down: the names the network gives its rooms, and their biomes.</summary>
    [Export] public WorldLayer[] Layers { get; set; } = Array.Empty<WorldLayer>();

    /// <summary>The world's items (keys, codes), each laid in one room by an <see cref="ItemPickup"/>.</summary>
    [Export] public ItemDefinition[] Items { get; set; } = Array.Empty<ItemDefinition>();

    /// <summary>The world's own lines, extending the game's own book (its <see cref="LineBook.Base"/>): with it, every line the world shows.</summary>
    [Export] public LineBook Lines { get; set; } = null!;

    /// <summary>The save's file name (user://&lt;name&gt;.json): each world keeps its own.</summary>
    [Export] public string SaveName { get; set; } = "save";

    /// <summary>Where this world's save is written (<see cref="SaveFile"/>).</summary>
    public string SavePath => $"user://{SaveName}.json";

    /// <summary>The scene path of the room named <paramref name="name"/>.</summary>
    public string RoomPath(string name) => $"{RoomsFolder}/{name}.tscn";

    /// <summary>The scene paths of every room of the world, in name order: the scenes of <see cref="RoomsFolder"/>.</summary>
    public IEnumerable<string> RoomScenes() =>
        DirAccess.GetFilesAt(RoomsFolder).Where(f => f.EndsWith(".tscn", StringComparison.Ordinal)).Order().Select(f => $"{RoomsFolder}/{f}");

    /// <summary>Reads and parses the world's network (<see cref="Network"/>).</summary>
    public WorldData<MapDraft> LoadNetwork() => Read(Network, MapDraft.FromMarkdown);

    /// <summary>Reads and parses the world's room table (<see cref="RoomTable"/>).</summary>
    public WorldData<RoomTable> LoadRoomTable() => Read(RoomTable, TheDeep.RoomTable.Parse);

    /// <summary>The one place a world's data files are opened: <paramref name="path"/> read as text and given to <paramref name="parse"/>.</summary>
    private static WorldData<T> Read<T>(string path, Func<string, T> parse)
    {
        if (path == "" || !FileAccess.FileExists(path))
        {
            return new WorldData<T>.Missing();
        }

        try
        {
            return new WorldData<T>.Loaded(parse(FileAccess.GetFileAsString(path)));
        }
        catch (FormatException e)
        {
            return new WorldData<T>.Malformed(e.Message);
        }
    }

    /// <summary>The layer named <paramref name="name"/> (as the network names it, any case), or null.</summary>
    public WorldLayer? Layer(string name)
    {
        foreach (WorldLayer layer in Layers)
        {
            if (string.Equals(layer.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return layer;
            }
        }

        return null;
    }

    /// <summary>The item whose id is <paramref name="id"/>, or null (an id no item of the world has: the room check reports it).</summary>
    public ItemDefinition? Item(string id)
    {
        foreach (ItemDefinition item in Items)
        {
            if (item.Id == id)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>The world named <paramref name="name"/> (its folder under <see cref="Folder"/>), or null if there is none.</summary>
    public static WorldDefinition? Load(string name)
    {
        string path = $"{Folder}/{name}/{FileName}";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<WorldDefinition>(path) : null;
    }
}
