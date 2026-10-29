using System;
using Godot;

namespace TheDeep;

/// <summary>
/// A boss's arena: when the pilgrim steps inside it while the boss stands, the gates fall behind and ahead; when the
/// boss breaks, they rise and stay up. Once beaten, a boss is gone for good (<see cref="Session"/> remembers it by
/// this node's room and name, and calls <see cref="Settle"/> when the room is entered again). Dying in the fight
/// reloads the room, boss and gates renewed. The gates are not part of the map: the ways stay open on paper.
/// </summary>
public partial class BossSeal : Node3D
{
    [Export] public Machine Boss { get; set; } = null!;

    [Export] public Portcullis[] Gates { get; set; } = Array.Empty<Portcullis>();

    /// <summary>The arena on the play plane, room-local: the fight starts when the pilgrim's feet are inside.</summary>
    [Export] public Vector2 ArenaMin { get; set; }
    [Export] public Vector2 ArenaMax { get; set; }

    /// <summary>Banner lines when the fight starts (the boss's name) and when it ends.</summary>
    [Export] public StringName NameLine { get; set; } = "";
    [Export] public StringName FallenLine { get; set; } = "";
    [Export] public StringName FallenHintLine { get; set; } = "";

    private enum Phase { Waiting, Fighting, Won }

    private Phase _phase;

    /// <summary>The pilgrim, set by the session when the room loads (it lives outside the room's scene).</summary>
    public Player? Player { get; set; }

    public bool Fighting => _phase == Phase.Fighting;

    /// <summary>The fight began: the gates are falling.</summary>
    public event Action<BossSeal>? Began;

    /// <summary>The boss broke: the gates are rising.</summary>
    public event Action<BossSeal>? Defeated;

    /// <summary>Beaten before: no boss, the gates up.</summary>
    public void Settle()
    {
        _phase = Phase.Won;
        Boss.Gone();
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (_phase)
        {
            case Phase.Waiting when Player is { } player && Inside(player) && !Boss.Broken && player.InControl:
                _phase = Phase.Fighting;
                foreach (Portcullis gate in Gates)
                {
                    gate.Close();
                }

                Began?.Invoke(this);
                break;
            case Phase.Fighting when Boss.Broken:
                _phase = Phase.Won;
                foreach (Portcullis gate in Gates)
                {
                    gate.Open();
                }

                Defeated?.Invoke(this);
                break;
        }
    }

    private bool Inside(Player player)
    {
        Vector3 feet = player.GlobalPosition;
        return feet.X >= ArenaMin.X && feet.X <= ArenaMax.X && feet.Y >= ArenaMin.Y && feet.Y <= ArenaMax.Y;
    }
}
