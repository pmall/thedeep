using System;
using Godot;

namespace TheDeep;

/// <summary>
/// Holds the current room. Rooms are separate scenes, loaded here as children; their connections are
/// <see cref="RoomExit"/> markers inside them. Transitions between rooms come with M6.
/// </summary>
public partial class World : Node3D
{
    /// <summary>Folder of the room scenes; a room's name is its file name there.</summary>
    public const string RoomFolder = "res://scenes/rooms";

    [Export] public PackedScene StartRoom { get; set; } = null!;

    private Room? _room;

    /// <summary>The loaded room. Set before any sibling after this node is ready.</summary>
    public Room Room => _room ?? throw new InvalidOperationException("no room loaded yet");

    /// <summary>Raised after a room is loaded and ready.</summary>
    public event Action<Room>? RoomLoaded;

    public override void _Ready()
    {
        Load(StartRoom);
    }

    /// <summary>Replaces the current room with a new instance of <paramref name="scene"/>.</summary>
    public void Load(PackedScene scene)
    {
        if (_room is not null)
        {
            RemoveChild(_room);
            _room.QueueFree();
        }

        _room = scene.Instantiate<Room>();
        AddChild(_room);
        RoomLoaded?.Invoke(_room);
    }

    /// <summary>The scene path of the room named <paramref name="name"/> (its file name without extension).</summary>
    public static string RoomPath(string name) => $"{RoomFolder}/{name}.tscn";
}
