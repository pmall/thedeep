using System;

namespace TheDeep;

/// <summary>Which abilities the player has unlocked: simple flags. Pure logic, unit tested.</summary>
public sealed class AbilitySet
{
    /// <summary>Every ability the pilgrim can unlock, one flag each.</summary>
    public static readonly Ability[] All =
    {
        Ability.MagGrip, Ability.Harpoon, Ability.Drill, Ability.Ballast, Ability.HeatPlating,
    };

    /// <summary>The flags of the abilities the pilgrim has now.</summary>
    public Ability Unlocked { get; private set; }

    /// <summary>Raised with the ability and its new state whenever a flag actually changes.</summary>
    public event Action<Ability, bool>? Changed;

    /// <summary>Whether <paramref name="ability"/> is unlocked; never true for <see cref="Ability.None"/>.</summary>
    public bool Has(Ability ability) => ability != Ability.None && (Unlocked & ability) == ability;

    /// <summary>Gives the pilgrim <paramref name="ability"/>.</summary>
    public void Unlock(Ability ability) => Set(ability, true);

    /// <summary>Takes <paramref name="ability"/> away from the pilgrim.</summary>
    public void Lock(Ability ability) => Set(ability, false);

    /// <summary>Unlocks <paramref name="ability"/> if it is locked, locks it if it is unlocked.</summary>
    public void Toggle(Ability ability) => Set(ability, !Has(ability));

    /// <summary>Unlocks or locks <paramref name="ability"/>; does nothing, and raises nothing, when it is already in that
    /// state.</summary>
    public void Set(Ability ability, bool unlocked)
    {
        if (ability == Ability.None || Has(ability) == unlocked)
        {
            return;
        }

        Unlocked = unlocked ? Unlocked | ability : Unlocked & ~ability;
        Changed?.Invoke(ability, unlocked);
    }
}
