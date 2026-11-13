using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A sealed arena, what a boss's (<see cref="BossSeal"/>) and an ambush's (<see cref="Ambush"/>) have in common: when the pilgrim, in
/// control, steps inside its area while its foes stand, the gates fall behind and ahead; when the last foe breaks, they rise and stay
/// up (the core's <see cref="ArenaFight"/>). Won, it is won for good (<see cref="Arenas"/> remembers it by this node's room and name,
/// and calls <see cref="Settle"/> when the room is entered again); dying in the fight reloads the room, foes and gates renewed. The
/// gates are not part of the map: the ways stay open on paper. The room check holds the area, its foes and its gates to the room
/// (<see cref="SealedArena"/>), and each arena to its world's network (a <c>boss</c> or an <c>ambush</c> line).
/// </summary>
public abstract partial class Arena : Node3D
{
    [Export] public Portcullis[] Gates { get; set; } = Array.Empty<Portcullis>();

    /// <summary>The arena's area on the play plane, room-local: the fight starts when the pilgrim's feet are inside.</summary>
    [Export] public Vector2 ArenaMin { get; set; }
    [Export] public Vector2 ArenaMax { get; set; }

    /// <summary>How hard the camera shakes as each gate slams down (<see cref="SideCamera.AddShake"/>).</summary>
    [Export] public float SlamShake { get; set; } = 0.3f;

    private readonly ArenaFight _fight = new();

    /// <summary>The pilgrim, set when the room loads (it lives outside the room's scene).</summary>
    public Player? Player { get; set; }

    /// <summary>The fight is on, not won yet.</summary>
    public bool Fighting => _fight.Phase == ArenaPhase.Fighting;

    /// <summary>The fight is won (this visit, or for good before).</summary>
    public bool Won => _fight.Phase == ArenaPhase.Won;

    /// <summary>The area on the play plane, as the core's box.</summary>
    public Box Area => new(ArenaMin.X, ArenaMin.Y, ArenaMax.X, ArenaMax.Y);

    /// <summary>The arena's foes, where the room places them (the room check reads them; not for every frame).</summary>
    public abstract IEnumerable<Machine> Foes { get; }

    /// <summary>The fight began: the gates are falling.</summary>
    public event Action<Arena>? Began;

    /// <summary>The last foe broke: the gates are rising.</summary>
    public event Action<Arena>? Cleared;

    /// <summary>Won before: no foe, the gates up.</summary>
    public virtual void Settle() => _fight.Settle();

    /// <summary>Some foe stands, or, while the arena waits (<paramref name="waiting"/>), some foe is there to spring.</summary>
    protected abstract bool FoesStand(bool waiting);

    /// <summary>The fight begins: what the foes do as the gates fall.</summary>
    protected virtual void OnBegan()
    {
    }

    public override void _PhysicsProcess(double delta)
    {
        bool pilgrimIn = Player is { InControl: true } player && Area.Contains(player.GlobalPosition.Flat());
        switch (_fight.Step(pilgrimIn, FoesStand(_fight.Phase == ArenaPhase.Waiting)))
        {
            case ArenaChange.Began:
                foreach (Portcullis gate in Gates)
                {
                    gate.Close();
                }

                OnBegan();
                Began?.Invoke(this);
                break;
            case ArenaChange.Cleared:
                foreach (Portcullis gate in Gates)
                {
                    gate.Open();
                }

                Cleared?.Invoke(this);
                break;
            case ArenaChange.None:
                break;
        }
    }
}
