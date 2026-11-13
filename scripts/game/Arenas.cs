using System;
using Godot;

namespace TheDeep;

/// <summary>
/// Sealed arenas in play (<see cref="Arena"/>): each is handed the pilgrim when its room loads, settled if it was won before, and
/// remembered for good the moment it is won (the session keeps it); a gate slamming shakes the camera, as an arena won does. A boss's
/// (<see cref="BossSeal"/>) shows its name on the banner when the fight begins and its fall when it breaks. How hard and how long
/// are each arena's own exports.
/// </summary>
public partial class Arenas : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public Session Session { get; set; } = null!;
    [Export] public Banner Banner { get; set; } = null!;
    [Export] public SideCamera Camera { get; set; } = null!;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree() => World.RoomLoaded -= OnRoomLoaded;

    private void OnRoomLoaded(Room room)
    {
        string name = World.NameOf(room.SceneFilePath);
        foreach (Arena arena in room.All<Arena>())
        {
            arena.Player = Player;
            if (Session.Done(name, arena.Name))
            {
                arena.Settle();
                continue;
            }

            arena.Began += OnBegan;
            arena.Cleared += OnCleared;
            foreach (Portcullis gate in arena.Gates)
            {
                gate.Slammed += _ => Camera.AddShake(arena.SlamShake);
            }
        }
    }

    private void OnBegan(Arena arena)
    {
        if (arena is BossSeal seal)
        {
            Banner.Show(seal.Named);
        }
    }

    /// <summary>An arena won: remembered (and saved) at once, so its foes never stand again.</summary>
    private void OnCleared(Arena arena)
    {
        Session.Remember(arena.Name);
        switch (arena)
        {
            case BossSeal seal:
                Camera.AddShake(seal.FallenShake);
                Banner.Show(seal.Fallen);
                break;
            case Ambush ambush:
                Camera.AddShake(ambush.ClearedShake);
                break;
            default:
                throw new InvalidOperationException($"arena {arena.Name} is of no kind Arenas knows ({arena.GetType().Name})");
        }
    }
}
