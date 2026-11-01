using System.Collections.Generic;
using Godot;
using TheDeep.States;

namespace TheDeep;

/// <summary>
/// A gate the pilgrim can't pass yet says so (roadmap W1): pressing into a metal wall or ceiling without Mag-grip, a mooring ring
/// in sight without the Harpoon, against cracked rock without the Drill, pressing down in deep water without Ballast, thrown
/// back by heat without Heat plating. Meeting one for a moment shows the pilgrim's line about it (<see cref="GateKinds"/>) in
/// the <see cref="HintLine"/>, once per room visit for each kind.
/// </summary>
public partial class GateHints : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public HarpoonRig Harpoon { get; set; } = null!;
    [Export] public HintLine Hint { get; set; } = null!;

    /// <summary>Seconds of meeting a gate before the line shows (a brush against it says nothing).</summary>
    [Export] public float MeetTime { get; set; } = 0.3f;

    /// <summary>A ring is only remarked on after it has been in sight this long, seconds.</summary>
    [Export] public float RingTime { get; set; } = 0.8f;

    private readonly Dictionary<Ability, float> _met = new();
    private readonly HashSet<Ability> _said = new();

    /// <summary>Raised when the pilgrim says its line about a gate (the kind is the ability it lacks).</summary>
    public event System.Action<Ability>? Said;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        HeatZone.Scorched += OnScorched;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree()
    {
        World.RoomLoaded -= OnRoomLoaded;
        HeatZone.Scorched -= OnScorched;
    }

    private void OnScorched(HeatZone zone)
    {
        if (!Player.Abilities.Has(Ability.HeatPlating) && !_said.Contains(Ability.HeatPlating))
        {
            Say(Ability.HeatPlating);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Player.InControl)
        {
            return;
        }

        float dt = (float)delta;
        Meet(Ability.MagGrip, PressesBareMetal(), dt, MeetTime);
        Meet(Ability.Harpoon, Harpoon.OutOfReach is not null, dt, RingTime);
        Meet(Ability.Drill, TouchesCrackedRock(), dt, MeetTime);
        Meet(Ability.Ballast, Player.CurrentState is SwimState && Player.CurrentInput.MoveY < 0, dt, MeetTime);
    }

    private void Meet(Ability kind, bool meeting, float dt, float needed)
    {
        if (Player.Abilities.Has(kind) || _said.Contains(kind))
        {
            return;
        }

        float t = _met.GetValueOrDefault(kind);
        t = meeting ? t + dt : Mathf.Max(0f, t - 0.5f * dt);
        _met[kind] = t;
        if (t >= needed)
        {
            Say(kind);
        }
    }

    private void Say(Ability kind)
    {
        _said.Add(kind);
        Hint.Show(GateKinds.HintLine(kind), GateKinds.ColorOf(kind));
        Said?.Invoke(kind);
    }

    private bool PressesBareMetal()
    {
        if (!Player.IsUpright)
        {
            return false;
        }

        int x = Player.CurrentInput.MoveX;
        return (Player.IsOnWall() && x != 0 && Player.MetalContact(n => n.X * x < -0.5f) is not null)
               || (Player.IsOnCeiling() && Player.MetalContact(n => n.Y < -0.5f) is not null);
    }

    private bool TouchesCrackedRock()
    {
        for (int i = 0; i < Player.GetSlideCollisionCount(); i++)
        {
            using KinematicCollision3D collision = Player.GetSlideCollision(i); // disposed, so the engine reuses it
            if (collision.GetCollider() is Block { Cracked: true, Broken: false })
            {
                return true;
            }
        }

        return false;
    }

    private void OnRoomLoaded(Room _)
    {
        _met.Clear();
        _said.Clear();
        Hint.Clear();
    }
}
