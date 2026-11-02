using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Hidden passages in play: the pilgrim's body entering a <see cref="FalseWall"/> finds it (it fades away, remembered for good by
/// the session); found ones are gone when their room loads.
/// </summary>
public partial class Secrets : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public Session Session { get; set; } = null!;

    /// <summary>Raised when a false wall is found.</summary>
    public event System.Action<FalseWall>? Found;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree() => World.RoomLoaded -= OnRoomLoaded;

    public override void _PhysicsProcess(double delta)
    {
        IReadOnlyList<FalseWall> walls = World.Room.All<FalseWall>();
        Vector3 p = Player.GlobalPosition;
        var center = new Vector2(p.X, p.Y + 0.9f);
        for (int i = 0; i < walls.Count; i++)
        {
            FalseWall wall = walls[i];
            if (!wall.Found && wall.Region.HasPoint(center))
            {
                wall.Reveal();
                Session.Remember(wall.Name);
                Found?.Invoke(wall);
            }
        }
    }

    private void OnRoomLoaded(Room room)
    {
        string name = World.NameOf(room.SceneFilePath);
        foreach (FalseWall wall in room.All<FalseWall>())
        {
            if (Session.Done(name, wall.Name))
            {
                wall.Settle();
            }
        }
    }
}
