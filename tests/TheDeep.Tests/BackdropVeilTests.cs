using System.Linq;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class BackdropVeilTests
{
    [Fact]
    public void EachRowDeeperIsAClearStepMoreVeiled()
    {
        float[] veils = BackdropRowSpec.Defaults.Select(r => BackdropVeil.Amount(r.Depth)).ToArray();
        for (int i = 1; i < veils.Length; i++)
        {
            Assert.True(veils[i] - veils[i - 1] > 0.1f);
        }

        Assert.All(veils, v => Assert.InRange(v, 0.15f, 0.9f));
        Assert.Equal(0f, BackdropVeil.Amount(0f));
    }

    [Theory]
    [InlineData(0.15f, 0.125f, 0.11f, 0.145f)] // a haze brighter than the dark basalt's share
    [InlineData(0.29f, 0.32f, 0.42f, 0.5f)] // a haze well under pale rock
    [InlineData(0.9f, 0.9f, 0.95f, 0.4f)] // a white haze
    public void TheVeilIsTheHazeNeverBrighterThanTheRock(float r, float g, float b, float rock)
    {
        var fog = new Vector3(r, g, b);
        Vector3 veil = BackdropVeil.Color(fog, new Vector3(rock));
        Assert.True(BiomeRules.Value(veil) <= BackdropVeil.UnderRock * rock + 1e-4f);
        // The fog's own hue, only dimmed.
        Assert.Equal(Vector3.Normalize(fog).X, Vector3.Normalize(veil).X, 3);
        if (BiomeRules.Value(fog) <= BackdropVeil.UnderRock * rock)
        {
            Assert.Equal(fog, veil);
        }
    }
}
