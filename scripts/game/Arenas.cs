using Godot;

namespace TheDeep;

/// <summary>
/// Sealed arenas in play: a boss's (<see cref="BossSeal"/>: its name when the fight begins, its fall when it breaks) and an
/// ambush's (<see cref="Ambush"/>). Each is handed the pilgrim when its room loads, settled if it was won before, and remembered
/// for good the moment it is won (the session keeps it); a gate slamming shakes the camera.
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
        foreach (BossSeal seal in room.All<BossSeal>())
        {
            seal.Player = Player;
            if (Session.Done(name, seal.Name))
            {
                seal.Settle();
                continue;
            }

            seal.Began += s => _ = Banner.Show(s.NameLine, "", 2.5f);
            seal.Defeated += OnBossDefeated;
            Shake(seal.Gates);
        }

        foreach (Ambush ambush in room.All<Ambush>())
        {
            ambush.Player = Player;
            if (Session.Done(name, ambush.Name))
            {
                ambush.Settle();
                continue;
            }

            ambush.Won += a =>
            {
                Session.Remember(a.Name);
                Camera.AddShake(0.35f);
            };
            Shake(ambush.Gates);
        }
    }

    /// <summary>A boss broken: remembered (and saved) at once, so it never stands again.</summary>
    private void OnBossDefeated(BossSeal seal)
    {
        Session.Remember(seal.Name);
        Camera.AddShake(0.5f);
        _ = Banner.Show(seal.FallenLine, seal.FallenHintLine, 4f);
    }

    private void Shake(Portcullis[] gates)
    {
        foreach (Portcullis gate in gates)
        {
            gate.Slammed += _ => Camera.AddShake(0.3f);
        }
    }
}
