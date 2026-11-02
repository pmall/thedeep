using Xunit;

namespace TheDeep.Tests;

public class ProwessTests
{
    [Fact]
    public void StrengthAddsDamageAndDefenseAddsHealth()
    {
        Prowess p = Prowess.None.With(PowerStat.Strength, 1).With(PowerStat.Defense, 2).With(PowerStat.Strength, 1);

        Assert.Equal(new Prowess(2, 2), p);
        Assert.Equal(3, p.AttackDamage(1));
        Assert.Equal(7, p.MaxHealth(5));
    }

    [Fact]
    public void NoneChangesNothing()
    {
        Assert.Equal(1, Prowess.None.AttackDamage(1));
        Assert.Equal(5, Prowess.None.MaxHealth(5));
    }

    [Fact]
    public void TheSaveRoundTripsIt()
    {
        var save = new SaveData { Room = "Narthex", Strength = 1, Defense = 3 };

        SaveData? back = SaveData.FromJson(save.ToJson());

        Assert.NotNull(back);
        Assert.Equal(new Prowess(1, 3), new Pilgrimage(back).Prowess);
    }
}
