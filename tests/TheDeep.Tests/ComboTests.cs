using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class ComboTests
{
    private static Combo Make() => new(new Dictionary<StrikeKind, StrikeShape>
    {
        [StrikeKind.Swing] = new(StrikeKind.Swing, 1.8f, 1.5f, Vector2.UnitX, 0, 3f, 0.04f, 0f, 0.1f, 1f),
        [StrikeKind.Second] = new(StrikeKind.Second, 1.8f, 1.5f, Vector2.UnitX, 0, 3f, 0.04f, 0f, 0.1f, 1f),
        [StrikeKind.Finisher] = new(StrikeKind.Finisher, 2.3f, 1.8f, Vector2.UnitX, 1, 7f, 0.08f, 0f, 0.1f, 1f),
        [StrikeKind.Down] = new(StrikeKind.Down, 1.6f, 1.4f, -Vector2.UnitY, 0, 2f, 0.04f, 9f, 0.1f, 1f),
        [StrikeKind.Dash] = new(StrikeKind.Dash, 2.6f, 1.2f, Vector2.UnitX, 1, 5f, 0.06f, 0f, 0.1f, 1f),
    }, chainWindow: 0.25f);

    [Fact]
    public void PressesInTheWindowChainToTheFinisherThenStartOver()
    {
        Combo combo = Make();

        Assert.Equal(StrikeKind.Swing, combo.Next(false, false, false).Kind);
        combo.Ended();
        combo.Tick(0.1f);
        Assert.Equal(StrikeKind.Second, combo.Next(false, false, false).Kind);
        combo.Ended();
        Assert.Equal(StrikeKind.Finisher, combo.Next(false, false, false).Kind);
        combo.Ended();
        Assert.Equal(StrikeKind.Swing, combo.Next(false, false, false).Kind);
    }

    [Fact]
    public void APauseStartsTheChainOver()
    {
        Combo combo = Make();
        combo.Next(false, false, false);
        combo.Ended();
        combo.Tick(0.3f);

        Assert.Equal(StrikeKind.Swing, combo.Next(false, false, false).Kind);
    }

    [Fact]
    public void ADashGivesADashStrikeAndTheAirAndDownADownStrike()
    {
        Combo combo = Make();

        Assert.Equal(StrikeKind.Dash, combo.Next(dashing: true, airborne: true, holdingDown: true).Kind);
        Assert.Equal(StrikeKind.Down, combo.Next(false, airborne: true, holdingDown: true).Kind);
        Assert.Equal(StrikeKind.Swing, combo.Next(false, airborne: true, holdingDown: false).Kind);
        Assert.Equal(StrikeKind.Swing, combo.Next(false, airborne: false, holdingDown: true).Kind);
    }

    [Fact]
    public void ADownStrikeBreaksTheChain()
    {
        Combo combo = Make();
        combo.Next(false, false, false);
        combo.Ended();
        combo.Next(false, true, true);
        combo.Ended();

        Assert.Equal(StrikeKind.Swing, combo.Next(false, false, false).Kind);
    }

    [Fact]
    public void EveryKindNeedsAShape()
    {
        Assert.Throws<System.ArgumentException>(() => new Combo(new Dictionary<StrikeKind, StrikeShape>(), 0.25f));
    }
}
