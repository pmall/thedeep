using System.Collections.Generic;
using Godot;
using TheDeep.States;

namespace TheDeep;

/// <summary>
/// A gate the pilgrim can't pass yet says so: pressing into a metal wall or ceiling without Mag-grip, a mooring ring
/// in sight without the Harpoon, against cracked rock without the Drill, pressing down in deep water without Ballast, thrown
/// back by heat without Heat plating. Meeting one for a moment shows the pilgrim's line about it (<see cref="GateKinds"/>) in
/// the <see cref="HintLine"/>, once per room visit for each kind. A law pressed on says why the pilgrim may not, the same way
/// (<see cref="LawBreak.Refusal"/>), and the refusal is withdrawn the moment the law breaks: its reason is gone.
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

    /// <summary>How fast the meeting of a gate is forgotten when it stops, as a share of real time (a brush now and then never adds up).</summary>
    [Export] public float MeetDecay { get; set; } = 0.5f;

    /// <summary>How squarely a surface must face the pilgrim to be pressed against: the normal's part along the pressing direction.</summary>
    private const float PressNormal = 0.5f;

    private static readonly System.Func<System.Numerics.Vector2, bool> WallOnRight = n => n.X < -PressNormal;
    private static readonly System.Func<System.Numerics.Vector2, bool> WallOnLeft = n => n.X > PressNormal;
    private static readonly System.Func<System.Numerics.Vector2, bool> CeilingAbove = n => n.Y < -PressNormal;

    private readonly Dictionary<Ability, float> _met = new();
    private readonly HashSet<Ability> _said = new();

    /// <summary>Raised when the pilgrim says its line about a gate (the kind is the ability it lacks).</summary>
    public event System.Action<Ability>? Said;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        HeatColumn.Scorched += OnScorched;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree()
    {
        World.RoomLoaded -= OnRoomLoaded;
        HeatColumn.Scorched -= OnScorched;
    }

    private void OnScorched(HeatColumn zone)
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
        // A gate is only looked for while its line is still to come.
        if (Pending(Ability.MagGrip))
        {
            Meet(Ability.MagGrip, PressesBareMetal(), dt, MeetTime);
        }

        if (Pending(Ability.Harpoon))
        {
            Meet(Ability.Harpoon, Harpoon.OutOfReach is not null, dt, RingTime);
        }

        if (Pending(Ability.Drill))
        {
            Meet(Ability.Drill, TouchesCrackedRock(), dt, MeetTime);
        }

        if (Pending(Ability.Ballast))
        {
            Meet(Ability.Ballast, Player.CurrentState is SwimState && Player.CurrentInput.MoveY < 0, dt, MeetTime);
        }
    }

    /// <summary>Whether the pilgrim still lacks <paramref name="kind"/> and has not said its line about it this visit.</summary>
    private bool Pending(Ability kind) => !Player.Abilities.Has(kind) && !_said.Contains(kind);

    private void Meet(Ability kind, bool meeting, float dt, float needed)
    {
        float t = _met.GetValueOrDefault(kind);
        t = meeting ? t + dt : Mathf.Max(0f, t - MeetDecay * dt);
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

        // Static predicates, one per side: a lambda capturing the direction would allocate every frame.
        int x = Player.CurrentInput.MoveX;
        return (Player.IsOnWall() && x != 0 && Player.MetalContact(x > 0 ? WallOnRight : WallOnLeft) is not null)
               || (Player.IsOnCeiling() && Player.MetalContact(CeilingAbove) is not null);
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

    private void OnRefusal(LawBreak law) => Hint.Show(law.RefusalLine, LawBreak.WordsColor);

    private void OnBroke(LawBreak law) => Hint.Withdraw(law.RefusalLine, LawBreak.WordsColor);

    private void OnRoomLoaded(Room room)
    {
        foreach (LawBreak law in room.All<LawBreak>())
        {
            law.Refusal += OnRefusal;
            law.Broke += OnBroke;
        }

        _met.Clear();
        _said.Clear();
        Hint.Clear();
    }
}
