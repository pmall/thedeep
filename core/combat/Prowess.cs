using System;

namespace TheDeep;

/// <summary>
/// What the power-ups taken add to the pilgrim, for good (<see cref="PowerStat"/>): <see cref="Strength"/> more damage per blow,
/// <see cref="Defense"/> more health. Pure, a value; the save keeps it.
/// </summary>
public readonly record struct Prowess(int Strength, int Defense)
{
    /// <summary>No power-up taken: nothing added to damage or health.</summary>
    public static readonly Prowess None = new(0, 0);

    /// <summary>With <paramref name="amount"/> more of <paramref name="stat"/>.</summary>
    public Prowess With(PowerStat stat, int amount) => stat switch
    {
        PowerStat.Strength => this with { Strength = Strength + amount },
        PowerStat.Defense => this with { Defense = Defense + amount },
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
    };

    /// <summary>A blow's damage, from the moveset's own (<paramref name="baseDamage"/>).</summary>
    public int AttackDamage(int baseDamage) => baseDamage + Strength;

    /// <summary>The health's maximum, from the moveset's own (<paramref name="baseHealth"/>).</summary>
    public int MaxHealth(int baseHealth) => baseHealth + Defense;
}
