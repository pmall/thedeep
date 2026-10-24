using System.Collections.Generic;
using Xunit;

namespace TheDeep.Tests;

public class AbilitySetTests
{
    [Fact]
    public void Starts_with_nothing_unlocked()
    {
        var set = new AbilitySet();
        foreach (Ability ability in AbilitySet.All)
        {
            Assert.False(set.Has(ability));
        }
    }

    [Fact]
    public void Unlock_lock_and_toggle_affect_only_that_ability()
    {
        var set = new AbilitySet();
        set.Unlock(Ability.Drill);
        Assert.True(set.Has(Ability.Drill));
        Assert.False(set.Has(Ability.MagGrip));

        set.Toggle(Ability.MagGrip);
        Assert.True(set.Has(Ability.MagGrip));
        set.Lock(Ability.Drill);
        Assert.False(set.Has(Ability.Drill));
        Assert.True(set.Has(Ability.MagGrip));
    }

    [Fact]
    public void Changed_fires_only_on_real_changes()
    {
        var set = new AbilitySet();
        var events = new List<(Ability, bool)>();
        set.Changed += (ability, unlocked) => events.Add((ability, unlocked));

        set.Unlock(Ability.Harpoon);
        set.Unlock(Ability.Harpoon);
        set.Lock(Ability.Ballast);
        set.Toggle(Ability.Harpoon);

        Assert.Equal(new[] { (Ability.Harpoon, true), (Ability.Harpoon, false) }, events);
    }

    [Fact]
    public void None_is_never_had()
    {
        var set = new AbilitySet();
        set.Unlock(Ability.None);
        Assert.False(set.Has(Ability.None));
        Assert.Equal(Ability.None, set.Unlocked);
    }

    [Fact]
    public void All_lists_every_ability_once()
    {
        Ability combined = Ability.None;
        foreach (Ability ability in AbilitySet.All)
        {
            Assert.Equal(Ability.None, combined & ability);
            combined |= ability;
        }

        Assert.Equal(Ability.MagGrip | Ability.Harpoon | Ability.Drill | Ability.Ballast | Ability.HeatPlating, combined);
    }
}
