using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Holds the current room of the world being played (<see cref="Definition"/>). Rooms are separate scenes, loaded here one at a
/// time at the world origin; their connections are <see cref="RoomExit"/> markers inside them (<see cref="RoomTravel"/> moves
/// the player through them). Also keeps the map of visited rooms, placed from the exits crossed.
/// </summary>
public partial class World : Node3D
{
    /// <summary>The world played: its rooms, network, layers, line book and start.</summary>
    [Export] public WorldDefinition Definition { get; set; } = null!;

    /// <summary>The room loaded first instead of the world's start room (the save's, or `--room`'s); set before this node is ready.</summary>
    public PackedScene? FirstRoom { get; set; }

    private Room? _room;

    /// <summary>
    /// Every scene built so far, kept for the session (scenes, not rooms: small). Let go, a scene's C# wrapper is finalized on the GC's
    /// thread while the main thread may be building from it again ("Handle is not initialized"), and the C# resources it held
    /// (biomes, seam profiles, species) fall to their wrapper alone, whose finalizer then races the next room taking them from the
    /// engine's cache (a FATAL gchandle.is_released). Every room a check or the game builds comes through <see cref="Scene"/>.
    /// </summary>
    private readonly Dictionary<string, PackedScene> _scenes = new();

    /// <summary>The loaded room. Set before any sibling after this node is ready.</summary>
    public Room Room => _room ?? throw new InvalidOperationException("no room loaded yet");

    /// <summary>The loaded room's name: its scene's file name.</summary>
    public string RoomName { get; private set; } = "";

    /// <summary>The rooms visited so far and where they sit.</summary>
    public MapState Map { get; } = new();

    /// <summary>Raised after a room is loaded and ready.</summary>
    public event Action<Room>? RoomLoaded;

    public override void _EnterTree()
    {
        // Before any node is ready: the props, banners and manuals of this world read their words from its line book, and write
        // them in its text style.
        LineBook.Use(Definition.Lines);
        TextStyle.Use(Definition.TextStyle);
    }

    public override void _Ready()
    {
        Load(FirstRoom ?? Definition.StartRoom);
    }

    /// <summary>
    /// Replaces the current room with a new instance of <paramref name="scene"/>. With <paramref name="map"/>,
    /// the room is entered on the map where it already is (or at the origin); otherwise the caller places
    /// it with <see cref="Enter"/>.
    /// </summary>
    public Room Load(PackedScene scene, bool map = true)
    {
        _scenes.TryAdd(scene.ResourcePath, scene);
        if (_room is not null)
        {
            // Freed now, and its C# wrappers finalized before the next room is built, while nothing else touches the engine:
            // finalized on the GC thread during a later room's setup, they intermittently crashed Godot (gchandle.is_released,
            // "Handle is not initialized"; the room check does the same). A room is loaded under the veil: the pause is unseen.
            RemoveChild(_room);
            _room.Free();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        _room = scene.Instantiate<Room>();
        RoomName = NameOf(_room.SceneFilePath); // kept: read every frame, and the engine's string is converted anew on each read
        AddChild(_room);
        if (map)
        {
            Enter(_room, Map.Find(NameOf(scene.ResourcePath))?.Offset ?? NVector2.Zero);
        }

        RoomLoaded?.Invoke(_room);
        return _room;
    }

    /// <summary>Records <paramref name="room"/> on the map at <paramref name="offset"/> (kept if it was placed before).</summary>
    public void Enter(Room room, NVector2 offset)
    {
        string name = NameOf(room.SceneFilePath);
        Map.Enter(name, offset, room.BoundsMin.ToCore(), room.BoundsMax.ToCore());
        Map.SetWays(name, room.Exits().Where(e => !e.ArrivalOnly)
            .Select(e => new MapWay(name, e.Name, e.RoomPosition.ToCore(), e.Side, e.Requires, e.Open, e.Hidden)));
    }

    /// <summary>The scene path of the world's room named <paramref name="name"/> (its file name without extension).</summary>
    public string RoomPath(string name) => Definition.RoomPath(name);

    /// <summary>The scene at <paramref name="path"/> (a room's, or the main scene for the room check), loaded once and kept (see <see cref="_scenes"/>).</summary>
    public PackedScene Scene(string path)
    {
        if (!_scenes.TryGetValue(path, out PackedScene? scene))
        {
            scene = GD.Load<PackedScene>(path);
            _scenes[path] = scene;
        }

        return scene;
    }

    /// <summary>The scene paths of every room of the world, in name order (the lab rooms too; see <see cref="Room.InGame"/>).</summary>
    public IEnumerable<string> RoomScenes() => Definition.RoomScenes();

    /// <summary>A room's name from its scene path.</summary>
    public static string NameOf(string scenePath) => scenePath.GetFile().GetBaseName();
}
