using System;
using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class RequirementTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("None")]
    public void EmptyNeedsNothing(string text)
    {
        Requirement r = Requirement.Parse(text);

        Assert.True(r.IsMetBy(Ability.None));
        Assert.Equal("", r.ToString());
    }

    [Fact]
    public void OrNeedsAnyOne()
    {
        Requirement r = Requirement.Parse("MagGrip | Harpoon");

        Assert.True(r.IsMetBy(Ability.MagGrip));
        Assert.True(r.IsMetBy(Ability.Harpoon));
        Assert.False(r.IsMetBy(Ability.Drill));
        Assert.False(r.IsMetBy(Ability.None));
    }

    [Fact]
    public void AndNeedsBoth()
    {
        Requirement r = Requirement.Parse("Drill & Ballast");

        Assert.False(r.IsMetBy(Ability.Drill));
        Assert.True(r.IsMetBy(Ability.Drill | Ability.Ballast | Ability.Harpoon));
    }

    [Fact]
    public void AndBindsTighterThanOr()
    {
        Requirement r = Requirement.Parse("MagGrip | Drill & Ballast");

        Assert.True(r.IsMetBy(Ability.MagGrip));
        Assert.False(r.IsMetBy(Ability.Drill));
        Assert.Equal(new[] { Ability.MagGrip, Ability.Drill | Ability.Ballast }, r.Options.Select(o => o.Abilities));
    }

    [Fact]
    public void ParenthesesDistribute()
    {
        Requirement r = Requirement.Parse("Drill & (Ballast | HeatPlating)");

        Assert.Equal("Drill & Ballast | Drill & HeatPlating", r.ToString());
        Assert.True(r.IsMetBy(Ability.Drill | Ability.HeatPlating));
        Assert.False(r.IsMetBy(Ability.Ballast | Ability.HeatPlating));
    }

    [Fact]
    public void RedundantAlternativesAreDropped()
    {
        Requirement r = Requirement.Parse("MagGrip & Harpoon | MagGrip | MagGrip");

        Assert.Equal(new[] { Ability.MagGrip }, r.Options.Select(o => o.Abilities));
    }

    [Fact]
    public void CanonicalTextParsesBackToTheSame()
    {
        Requirement r = Requirement.Parse("(Harpoon|MagGrip)&(Drill|Ballast)");

        Assert.Equal(r.Options, Requirement.Parse(r.ToString()).Options);
    }

    [Theory]
    [InlineData("MagGrip |", "expected an ability")]
    [InlineData("2Key", "starts with a digit")]
    [InlineData("(MagGrip", "missing ')'")]
    [InlineData("MagGrip Harpoon", "unexpected 'H'")]
    [InlineData("MagGrip + Harpoon", "unexpected '+'")]
    public void MistakesAreReported(string text, string message)
    {
        var e = Assert.Throws<FormatException>(() => Requirement.Parse(text));
        Assert.Contains(message, e.Message);
        Assert.False(Requirement.TryParse(text, out _, out string error));
        Assert.Contains(message, error);
    }

    [Fact]
    public void ANameThatIsNoAbilityIsAnItem()
    {
        Requirement r = Requirement.Parse("Drill & CryptKey | Harpoon");

        Assert.Equal(ItemSet.Of("CryptKey"), r.Items);
        Assert.True(r.IsMetBy(Ability.Harpoon));
        Assert.False(r.IsMetBy(Ability.Drill));
        Assert.True(r.IsMetBy(Ability.Drill, ItemSet.Of("CryptKey")));
        Assert.False(r.IsMetBy(Ability.None, ItemSet.Of("CryptKey", "Other")));
        Assert.Equal("Harpoon | Drill & CryptKey", r.ToString());
        Assert.Equal(r.Options, Requirement.Parse(r.ToString()).Options);
    }

    [Fact]
    public void AnItemAloneIsMetByHoldingIt()
    {
        Requirement r = Requirement.Parse("CryptKey");

        Assert.False(r.IsMetBy(Ability.None));
        Assert.True(r.IsMetBy(Ability.None, ItemSet.Of("CryptKey")));
    }

    [Fact]
    public void WithoutItemsIsWhatTheBodyMustDoToReachTheDoor()
    {
        Assert.True(Requirement.Parse("CryptKey").WithoutItems().IsMetBy(Ability.None));
        Assert.Equal("Drill", Requirement.Parse("Drill & CryptKey").WithoutItems().ToString());
        Assert.Equal("", Requirement.Parse("Drill | CryptKey").WithoutItems().ToString());
        Requirement plain = Requirement.Parse("MagGrip | Harpoon");
        Assert.Same(plain, plain.WithoutItems());
    }

    [Fact]
    public void AnItemAlternativeCoveredByLessIsDropped()
    {
        Requirement r = Requirement.Parse("CryptKey & Drill | CryptKey");

        Assert.Equal("CryptKey", r.ToString());
    }

    [Theory]
    [InlineData(0.25f, 0.5f, true)]
    [InlineData(0f, 0.5f, false)]     // edges at -0.25 and 0.25
    [InlineData(60.75f, 7.5f, true)]   // edges at 57 and 64.5
    [InlineData(10f, 4f, true)]
    [InlineData(10.1f, 4f, false)]
    [InlineData(2.6f, 0.5f, false)]    // edges at 2.35 and 2.85
    public void BlockEdgesSnapToTheGrid(float center, float size, bool expected)
    {
        Assert.Equal(expected, GridSnap.EdgesOnGrid(center, size, GridSnap.BlockStep));
    }
}
