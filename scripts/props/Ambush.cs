using System;
using Godot;

namespace TheDeep;

/// <summary>
/// A trap room's arena (a room kind, docs/vision.md#game-systems): its machines (<see cref="Wave"/>) are not there until the pilgrim
/// steps inside the arena; then the gates fall behind and ahead and the wave stands up at its places; when the last of it breaks,
/// the gates rise and stay up (the core's <see cref="ArenaFight"/>). Cleared, it is cleared for good (<see cref="Arenas"/> remembers
/// it and calls <see cref="Settle"/>); dying in it reloads the room, the trap set again. The gates are not part of the map: the ways
/// stay open on paper, as a boss's arena's do.
/// </summary>
public partial class Ambush : Node3D
{
    /// <summary>The machines that spring the trap, placed where they stand up.</summary>
    [Export] public Machine[] Wave { get; set; } = Array.Empty<Machine>();

    [Export] public Portcullis[] Gates { get; set; } = Array.Empty<Portcullis>();

    /// <summary>The arena on the play plane, room-local: the trap springs when the pilgrim's feet are inside.</summary>
    [Export] public Vector2 ArenaMin { get; set; }
    [Export] public Vector2 ArenaMax { get; set; }

    private readonly ArenaFight _fight = new();

    /// <summary>The pilgrim, set when the room loads (it lives outside the room's scene).</summary>
    public Player? Player { get; set; }

    public bool Fighting => _fight.Phase == ArenaPhase.Fighting;

    public bool Cleared => _fight.Phase == ArenaPhase.Won;

    /// <summary>The trap sprang: the gates are falling, the wave standing up.</summary>
    public event Action<Ambush>? Sprang;

    /// <summary>The last of the wave broke: the gates are rising.</summary>
    public event Action<Ambush>? Won;

    public override void _Ready()
    {
        foreach (Machine machine in Wave)
        {
            machine.Gone(); // not there until the trap springs
        }
    }

    /// <summary>Cleared before: the wave never comes, the gates up.</summary>
    public void Settle() => _fight.Settle();

    public override void _PhysicsProcess(double delta)
    {
        bool pilgrimIn = Player is { InControl: true } player && Inside(player);
        bool standing = _fight.Phase == ArenaPhase.Waiting ? Wave.Length > 0 : Array.Exists(Wave, m => !m.Broken);
        switch (_fight.Step(pilgrimIn, standing))
        {
            case ArenaChange.Began:
                foreach (Portcullis gate in Gates)
                {
                    gate.Close();
                }

                foreach (Machine machine in Wave)
                {
                    machine.Reset(); // stands up at its place
                }

                Sprang?.Invoke(this);
                break;
            case ArenaChange.Cleared:
                foreach (Portcullis gate in Gates)
                {
                    gate.Open();
                }

                Won?.Invoke(this);
                break;
            case ArenaChange.None:
                break;
        }
    }

    private bool Inside(Player player)
    {
        Vector3 feet = player.GlobalPosition;
        return feet.X >= ArenaMin.X && feet.X <= ArenaMax.X && feet.Y >= ArenaMin.Y && feet.Y <= ArenaMax.Y;
    }
}
