using System;
using System.Collections.Generic;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Runs the machines of the current room. Each physics tick it gathers what they can perceive (the
/// player and whether each machine can see them, impacts from the player's landings and dashes, the
/// machines around each one) and steps every machine in a fixed order, so runs are deterministic. A strike
/// that reaches the player raises <see cref="PlayerStruck"/>; when it hurts, it throws sparks, shakes the camera and holds the
/// machine still as long as the pilgrim's hurt stop.
/// </summary>
public partial class MachineDirector : Node
{
    /// <summary>Machines within this distance know about each other, meters.</summary>
    private const float NeighborRange = 12f;

    /// <summary>Impact speed of a dash (a landing's is its fall speed): enough to be heard close by, not to wake sleepers.</summary>
    private const float DashImpact = 5f;

    private static readonly Vector3 Chest = new(0f, 1f, 0f);

    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;

    /// <summary>Sparks where a blow lands.</summary>
    [Export] public BurstPool? HitSparks { get; set; }

    [Export] public SideCamera? Camera { get; set; }

    /// <summary>How far a machine's blow that hurts the pilgrim kicks the camera along it, meters.</summary>
    [Export] public float HurtKick { get; set; } = 0.16f;

    /// <summary>The kick (meters) and sparks a blow adds on top of its strike's when it breaks the machine.</summary>
    [Export] public float BreakKick { get; set; } = 0.05f;
    [Export] public float BreakSparks { get; set; } = 0.4f;

    private readonly List<Machine> _machines = new();
    private readonly List<Impact> _impacts = new();
    private readonly List<Impact> _heard = new();
    private readonly List<Neighbor> _neighbors = new();
    private PlaneRay? _sight;
    private PlaneRay Sight => _sight ??= PlaneRay.For(PhysicsLayers.Solid);

    /// <summary>Machines already hit by a swing (by the swing's number), so a swing hits each once.</summary>
    private readonly HashSet<(Machine, int)> _struck = new();

    /// <summary>False hides the player from every machine (the species check runs them alone).</summary>
    public bool PlayerPresent { get; set; } = true;

    /// <summary>The machines of the room it is in charge of.</summary>
    public IReadOnlyList<Machine> Machines => _machines;

    /// <summary>The pilgrim's swing hit a machine (true: and broke it).</summary>
    public event Action<Machine, bool>? MachineHit;

    /// <summary>A machine's blow landed on the player.</summary>
    public event Action<Machine>? PlayerStruck;

    public override void _Ready()
    {
        Sight.Attach(this);
        World.RoomLoaded += OnRoomLoaded;
        Player.Landed += OnLanded;
        Player.Dashed += OnDashed;
        Player.Attack.Striking += OnStriking;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree()
    {
        World.RoomLoaded -= OnRoomLoaded;
        Player.Landed -= OnLanded;
        Player.Dashed -= OnDashed;
        Player.Attack.Striking -= OnStriking;
    }

    /// <summary>Takes charge of a machine added after the room loaded (the species check spawns them).</summary>
    public void Add(Machine machine) => _machines.Add(machine);

    /// <summary>Lets go of a machine taken out of the room.</summary>
    public void Remove(Machine machine) => _machines.Remove(machine);

    /// <summary>Every machine back where it spawned, with a fresh brain.</summary>
    public void ResetMachines()
    {
        _impacts.Clear();
        foreach (Machine machine in _machines)
        {
            machine.Reset();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _heard.Clear();
        _heard.AddRange(_impacts);
        _impacts.Clear();

        Vector3 chest = Player.GlobalPosition + Chest;
        foreach (Machine machine in _machines)
        {
            if (machine.Broken)
            {
                machine.Step(dt, default);
                continue;
            }

            PlayerTrace? player = null;
            if (PlayerPresent)
            {
                bool inSight = !Sight.Cast(machine.SensorPosition, chest);
                player = new PlayerTrace(chest.Flat(), Player.Velocity.Flat(), inSight);
            }

            _neighbors.Clear();
            Vector2 here = machine.PlanePosition;
            foreach (Machine other in _machines)
            {
                if (other != machine && !other.Broken && other.PlanePosition.DistanceTo(here) <= NeighborRange)
                {
                    _neighbors.Add(new Neighbor(other.PlanePosition.ToCore(), other.Species.Id, other.Intent.Signal));
                }
            }

            machine.Step(dt, machine.Perceive(player, _heard, _neighbors));
            if (machine.Intent.Hit && PlayerPresent && machine.SensorPosition.DistanceTo(chest) <= machine.Species.StrikeRange)
            {
                if (Player.TakeHit(machine.Species.Damage, machine.PlanePosition.ToCore()))
                {
                    // It hurt: the blow lands, the machine held in its follow-through as long as the pilgrim.
                    HitSparks?.Emit(chest, 1f);
                    Camera?.Kick(new NVector2(chest.X >= machine.PlanePosition.X ? 1f : -1f, 0.3f) * HurtKick);
                    machine.Halt(Player.Tuning.HurtStop);
                }

                PlayerStruck?.Invoke(machine);
            }
        }
    }

    /// <summary>
    /// The pilgrim's strike covers <paramref name="area"/>: every machine it reaches takes one blow per strike (its damage and
    /// knockback the strike's, <paramref name="strike"/>); a landed blow holds the pilgrim and the machine still a moment (the hit
    /// stop), and a down strike's bounces the pilgrim up.
    /// </summary>
    private void OnStriking(Rect2 area, int swing, StrikeShape strike)
    {
        bool landed = false;
        foreach (Machine machine in _machines)
        {
            if (machine.Broken || !machine.BodyRect.Intersects(area) || !_struck.Add((machine, swing)))
            {
                continue;
            }

            Rect2 body = machine.BodyRect.Intersection(area);
            Vector2 at = body.GetCenter();
            bool broke = machine.TakeHit(Player.AttackDamage + strike.ExtraDamage, Player.GlobalPosition.X, strike.Knockback);
            machine.Halt(strike.HitStop);
            HitSparks?.Emit(new Vector3(at.X, at.Y, 0.3f), strike.Sparks + (broke ? BreakSparks : 0f));
            Camera?.Kick(Along(strike) * (strike.Kick + (broke ? BreakKick : 0f)));
            MachineHit?.Invoke(machine, broke);
            landed = true;
        }

        if (landed)
        {
            Player.HitStop(strike.HitStop);
            if (strike.Bounce > 0f)
            {
                Player.Bounce(strike.Bounce);
            }
        }
    }

    /// <summary>The way a strike goes on the play plane: down for a down strike, else the side it was swung to.</summary>
    private NVector2 Along(StrikeShape strike) => strike.Toward.Y < 0f ? new NVector2(0f, -1f) : new NVector2(Player.Attack.Facing, 0f);

    private void OnRoomLoaded(Room room)
    {
        _struck.Clear();
        _machines.Clear();
        _impacts.Clear();
        Collect(room);
    }

    private void Collect(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is Machine machine)
            {
                _machines.Add(machine);
            }
            else
            {
                Collect(child);
            }
        }
    }

    private void OnLanded(float fallSpeed) => _impacts.Add(new Impact(Player.GlobalPosition.Flat(), fallSpeed));

    private void OnDashed(int direction) => _impacts.Add(new Impact(Player.GlobalPosition.Flat(), DashImpact));
}
