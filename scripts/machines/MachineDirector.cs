using System;
using System.Collections.Generic;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Runs the machines of the current room. Each physics tick it gathers what they can perceive (the
/// player and whether each machine can see them, impacts from the player's landings and dashes, the
/// machines around each one) and steps every machine in a fixed order, so runs are deterministic. A strike
/// that lands near the player throws sparks, shakes the camera and raises <see cref="PlayerStruck"/>
/// (damage comes with M6).
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

    /// <summary>Camera shake added by a blow, 0 to 1.</summary>
    [Export] public float HitShake { get; set; } = 0.35f;

    private readonly List<Machine> _machines = new();
    private readonly List<Impact> _impacts = new();
    private readonly List<Impact> _heard = new();
    private readonly List<Neighbor> _neighbors = new();

    /// <summary>Machines already hit by a swing (by the swing's number), so a swing hits each once.</summary>
    private readonly HashSet<(Machine, int)> _struck = new();

    /// <summary>False hides the player from every machine (the species check runs them alone).</summary>
    public bool PlayerPresent { get; set; } = true;

    public IReadOnlyList<Machine> Machines => _machines;

    /// <summary>The pilgrim's swing hit a machine (true: and broke it).</summary>
    public event Action<Machine, bool>? MachineHit;

    /// <summary>A machine's blow landed on the player.</summary>
    public event Action<Machine>? PlayerStruck;

    public override void _Ready()
    {
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
        PhysicsDirectSpaceState3D space = Player.GetWorld3D().DirectSpaceState;
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
                var sight = PhysicsRayQueryParameters3D.Create(machine.SensorPosition, chest, PhysicsLayers.Solid);
                bool inSight = space.IntersectRay(sight).Count == 0;
                player = new PlayerTrace(Flat(chest), Flat(Player.Velocity), inSight);
            }

            _neighbors.Clear();
            Vector2 here = machine.PlanePosition;
            foreach (Machine other in _machines)
            {
                if (other != machine && !other.Broken && other.PlanePosition.DistanceTo(here) <= NeighborRange)
                {
                    _neighbors.Add(new Neighbor(Flat(other.PlanePosition), other.Species.Id, other.Intent.Signal));
                }
            }

            machine.Step(dt, machine.Perceive(player, _heard, _neighbors));
            if (machine.Intent.Hit && PlayerPresent && machine.SensorPosition.DistanceTo(chest) <= machine.Species.StrikeRange)
            {
                HitSparks?.Emit(chest, 1f);
                Camera?.AddShake(HitShake);
                Player.TakeHit(machine.Species.Damage, new System.Numerics.Vector2(machine.PlanePosition.X, machine.PlanePosition.Y));
                PlayerStruck?.Invoke(machine);
            }
        }
    }

    /// <summary>The pilgrim's swing covers <paramref name="area"/>: every machine it reaches takes one hit per swing.</summary>
    private void OnStriking(Rect2 area, int swing)
    {
        foreach (Machine machine in _machines)
        {
            if (machine.Broken || !machine.BodyRect.Intersects(area) || !_struck.Add((machine, swing)))
            {
                continue;
            }

            Rect2 body = machine.BodyRect.Intersection(area);
            Vector2 at = body.GetCenter();
            bool broke = machine.TakeHit(Player.Tuning.AttackDamage, new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Y),
                Player.Tuning.AttackKnockback);
            HitSparks?.Emit(new Vector3(at.X, at.Y, 0.3f), broke ? 1.5f : 0.7f);
            Camera?.AddShake(broke ? 0.3f : 0.12f);
            MachineHit?.Invoke(machine, broke);
        }
    }

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

    private void OnLanded(float fallSpeed) => _impacts.Add(new Impact(Flat(Player.GlobalPosition), fallSpeed));

    private void OnDashed(int direction) => _impacts.Add(new Impact(Flat(Player.GlobalPosition), DashImpact));

    private static NVector2 Flat(Vector3 v) => new(v.X, v.Y);

    private static NVector2 Flat(Vector2 v) => new(v.X, v.Y);
}
