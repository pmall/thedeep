using Xunit;

namespace TheDeep.Tests;

public class CombatTests
{
    [Fact]
    public void AHitTakesHealthThenGraceProtects()
    {
        var v = new Vitality(5, 1f);
        Assert.True(v.Damage(1));
        Assert.Equal(4, v.Current);
        Assert.False(v.Damage(1));
        v.Tick(0.5f);
        Assert.False(v.Damage(1));
        v.Tick(0.6f);
        Assert.True(v.Damage(2));
        Assert.Equal(2, v.Current);
    }

    [Fact]
    public void DeathAndRestore()
    {
        var v = new Vitality(2);
        int changes = 0;
        v.Changed += _ => changes++;
        v.Damage(5);
        Assert.True(v.Dead);
        Assert.False(v.Damage(1));
        v.Restore();
        Assert.Equal(2, v.Current);
        Assert.False(v.Dead);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void SwingGoesThroughItsPhases()
    {
        var s = new Swing(0.05f, 0.1f, 0.15f, 0.05f);
        Assert.Equal(SwingPhase.Ready, s.Phase);
        Assert.True(s.Press());
        Assert.Equal(SwingPhase.WindUp, s.Phase);
        s.Tick(0.06f);
        Assert.Equal(SwingPhase.Active, s.Phase);
        s.Tick(0.1f);
        Assert.Equal(SwingPhase.Recover, s.Phase);
        Assert.False(s.Press()); // queued
        s.Tick(0.15f);
        Assert.Equal(SwingPhase.Ready, s.Phase);
        Assert.True(s.Tick(0.06f)); // the queued swing starts after the cooldown
        Assert.Equal(2, s.Count);
    }

    [Fact]
    public void APressDuringTheHitIsQueued()
    {
        var s = new Swing(0.05f, 0.1f, 0.15f, 0.05f);
        s.Press();
        s.Tick(0.06f);
        Assert.Equal(SwingPhase.Active, s.Phase);
        Assert.False(s.Press());
        for (int i = 0; i < 20; i++)
        {
            s.Tick(0.02f);
        }

        Assert.Equal(2, s.Count);
    }

    [Fact]
    public void APressDuringTheWindUpIsNotQueued()
    {
        var s = new Swing(0.05f, 0.1f, 0.15f, 0f);
        s.Press();
        Assert.False(s.Press());
        for (int i = 0; i < 40; i++)
        {
            s.Tick(0.02f);
        }

        Assert.Equal(1, s.Count);
    }
}
