using System;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Holds the current room. Rooms are separate scenes, loaded here one at a time at the world origin;
/// their connections are <see cref="RoomExit"/> markers inside them (<see cref="RoomTravel"/> moves the
/// player through them). Also keeps the map of visited rooms, placed from the exits crossed.
/// </summary>
public partial class World : Node3D
{
    /// <summary>Folder of the room scenes; a room's name is its file name there.</summary>
    public const string RoomFolder = "res://scenes/rooms";

    [Export] public PackedScene StartRoom { get; set; } = null!;

    private Room? _room;

    /// <summary>The loaded room. Set before any sibling after this node is ready.</summary>
    public Room Room => _room ?? throw new InvalidOperationException("no room loaded yet");

    /// <summary>The loaded room's name: its scene's file name.</summary>
    public string RoomName => _room is null ? "" : NameOf(_room.SceneFilePath);

    /// <summary>The rooms visited so far and where they sit.</summary>
    public MapState Map { get; } = new();

    /// <summary>Raised after a room is loaded and ready.</summary>
    public event Action<Room>? RoomLoaded;

    public override void _Ready()
    {
        Load(StartRoom);
    }

    /// <summary>
    /// Replaces the current room with a new instance of <paramref name="scene"/>. With <paramref name="map"/>,
    /// the room is entered on the map where it already is (or at the origin); otherwise the caller places
    /// it with <see cref="Enter"/>.
    /// </summary>
    public Room Load(PackedScene scene, bool map = true)
    {
        if (_room is not null)
        {
            RemoveChild(_room);
            _room.QueueFree();
        }

        _room = scene.Instantiate<Room>();
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
        Map.Enter(NameOf(room.SceneFilePath), offset,
            new NVector2(room.BoundsMin.X, room.BoundsMin.Y), new NVector2(room.BoundsMax.X, room.BoundsMax.Y));
    }

    /// <summary>The scene path of the room named <paramref name="name"/> (its file name without extension).</summary>
    public static string RoomPath(string name) => $"{RoomFolder}/{name}.tscn";

    /// <summary>A room's name from its scene path.</summary>
    public static string NameOf(string scenePath) => scenePath.GetFile().GetBaseName();
}
