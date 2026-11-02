using Godot;

namespace TheDeep;

/// <summary>
/// One strike's numbers (<see cref="StrikeKind"/>; what each means is <see cref="StrikeShape"/>), a sub-resource of the tuning
/// (<see cref="PlayerTuning.Strikes"/>): the owner may edit them in player_tuning.tres and relaunch.
/// </summary>
[GlobalClass]
public partial class StrikeTuning : Resource
{
    [Export] public StrikeKind Kind { get; set; }

    /// <summary>Length along the strike (forward, or down for a down strike) and width across it, meters.</summary>
    [Export] public float Reach { get; set; } = 1.8f;
    [Export] public float Width { get; set; } = 1.5f;

    /// <summary>Strikes downward, under the feet (a down strike), instead of forward.</summary>
    [Export] public bool Downward { get; set; }

    /// <summary>Damage added to a blow's (<see cref="PlayerTuning.AttackDamage"/>).</summary>
    [Export] public int ExtraDamage { get; set; }

    /// <summary>Speed a hit machine is thrown back with, m/s.</summary>
    [Export] public float Knockback { get; set; } = 3f;

    /// <summary>Seconds the pilgrim and what it hit hold still when the blow lands.</summary>
    [Export] public float HitStop { get; set; } = 0.05f;

    /// <summary>Upward speed a hit gives the pilgrim (a down strike's bounce), m/s; 0 for none.</summary>
    [Export] public float Bounce { get; set; }

    public StrikeShape Shape => new(Kind, Reach, Width, Downward ? -System.Numerics.Vector2.UnitY : System.Numerics.Vector2.UnitX,
        ExtraDamage, Knockback, HitStop, Bounce);
}
