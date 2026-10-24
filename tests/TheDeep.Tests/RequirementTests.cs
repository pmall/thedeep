using System;
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
        Assert.Equal(new[] { Ability.MagGrip, Ability.Drill | Ability.Ballast }, r.Options);
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

        Assert.Equal(new[] { Ability.MagGrip }, r.Options);
    }

    [Fact]
    public void CanonicalTextParsesBackToTheSame()
    {
        Requirement r = Requirement.Parse("(Harpoon|MagGrip)&(Drill|Ballast)");

        Assert.Equal(r.Options, Requirement.Parse(r.ToString()).Options);
    }

    [Theory]
    [InlineData("Magrip", "unknown ability 'Magrip'")]
    [InlineData("MagGrip |", "expected an ability")]
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
