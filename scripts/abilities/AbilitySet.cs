using System;

namespace TheDeep;

/// <summary>Which abilities the player has unlocked: simple flags. Pure logic, unit tested.</summary>
public sealed class AbilitySet
{
    public static readonly Ability[] All =
    {
        Ability.MagGrip, Ability.Harpoon, Ability.Drill, Ability.Ballast, Ability.HeatPlating,
    };

    public Ability Unlocked { get; private set; }

    /// <summary>Raised with the ability and its new state whenever a flag actually changes.</summary>
    public event Action<Ability, bool>? Changed;

    public bool Has(Ability ability) => ability != Ability.None && (Unlocked & ability) == ability;

    public void Unlock(Ability ability) => Set(ability, true);

    public void Lock(Ability ability) => Set(ability, false);

    public void Toggle(Ability ability) => Set(ability, !Has(ability));

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
