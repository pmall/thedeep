using Xunit;

namespace TheDeep.Tests;

public class LawStrainTests
{
    private const float Dt = 1f / 60f;

    private static LawStrain Law() => new(new LawStrainTuning(Near: 6f, Lip: 1.2f, BehindLip: 0.2f, Height: 1.5f, PushStrain: 0.5f, PushTime: 0.5f, ShowRate: 1.5f, ReleaseRate: 0.5f, BreakTime: 1.2f));

    private static int Press(LawStrain law, float seconds, float away = 0.5f, bool standing = true, bool pushing = true, float hold = 0f)
    {
        int refusals = 0;
        for (int i = 0; i < (int)(seconds / Dt); i++)
        {
            if (law.Step(Dt, away, standing, pushing, hold))
            {
                refusals++;
            }
        }

        return refusals;
    }

    [Fact]
    public void WordsShowOnlyNearTheLip()
    {
        var law = Law();
        Press(law, 2f, away: 20f, pushing: false);
        Assert.Equal(0f, law.Shown);
        Press(law, 2f, away: 3f, pushing: false);
        Assert.Equal(1f, law.Shown);
    }

    [Fact]
    public void PressingStrainsUpToTheCapAndRefusesOnce()
    {
        var law = Law();
        int refusals = Press(law, 2f);
        Assert.Equal(1, refusals);
        Assert.True(law.Refused);
        Assert.True(law.Offering);
        Assert.Equal(0.5f, law.Strain, 3);
        Assert.False(law.Broken);
    }

    [Fact]
    public void NoPressingFromAFarOrWhenNotStandingDoesNotStrain()
    {
        var law = Law();
        Press(law, 2f, away: 2f);
        Press(law, 2f, standing: false);
        Assert.Equal(0f, law.Strain);
        Assert.False(law.Refused);
    }

    [Fact]
    public void StrainEasesOffWhenPressingStops()
    {
        var law = Law();
        Press(law, 0.25f);
        float held = law.Strain;
        Press(law, 0.25f, pushing: false);
        Assert.InRange(law.Strain, 0f, held - 0.01f);
    }

    [Fact]
    public void TheHoldRaisesTheStrainAndBreakingRunsToBroken()
    {
        var law = Law();
        Press(law, 0.2f, hold: 0.9f);
        Assert.Equal(0.9f, law.Strain, 3);
        law.Break();
        Assert.Equal(LawState.Breaking, law.State);
        Assert.True(law.Broken);
        Assert.Equal(1f, law.Strain);
        law.Advance(0.6f);
        Assert.Equal(0.5f, law.Gone, 3);
        law.Advance(0.7f);
        Assert.Equal(LawState.Broken, law.State);
        Assert.Equal(1f, law.Gone);
    }

    [Fact]
    public void SettleLeavesTheWayOpenAtOnce()
    {
        var law = Law();
        law.Settle();
        Assert.Equal(LawState.Broken, law.State);
        Assert.Equal(1f, law.Gone);
        Assert.Equal(0f, law.Strain);
    }
}
