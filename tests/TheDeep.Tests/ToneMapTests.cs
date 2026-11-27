using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class ToneMapTests
{
    private static readonly Vector3 Ink = new(0.05f, 0.03f, 0.1f);
    private static readonly Vector3 Mid = new(0.4f, 0.4f, 0.5f);
    private static readonly Vector3 Paper = new(0.95f, 0.92f, 0.85f);

    private static void Near(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Z, actual.Z, 3);
    }

    [Fact]
    public void NoAmountLeavesEveryColorAsItIs()
    {
        var tone = new ToneMap(Ink, Mid, Paper, 0f, 0f, 1f);
        var color = new Vector3(0.3f, 0.6f, 0.1f);
        Near(color, tone.Apply(color));
    }

    [Fact]
    public void BlackAndWhitePrintInTheShadowAndTheLightInk()
    {
        var tone = new ToneMap(Ink, Mid, Paper, 1f, 0f, 1f);
        Near(Ink, tone.Apply(Vector3.Zero));
        Near(Paper, tone.Apply(Vector3.One));
    }

    [Fact]
    public void AGreyAtTheMiddlePrintsInTheMiddleInk()
    {
        var tone = new ToneMap(Ink, Mid, Paper, 1f, 0f, 1f);
        Near(Mid, tone.Apply(new Vector3(ToneMap.MidValue)));
    }

    [Fact]
    public void ContrastPushesValuesAwayFromTheMiddle()
    {
        var flat = new ToneMap(Vector3.Zero, new Vector3(0.5f), Vector3.One, 1f, 0f, 1f);
        var hard = flat with { Contrast = 2f };
        Vector3 dark = new(0.3f);
        Assert.True(hard.Apply(dark).X < flat.Apply(dark).X);
    }

    [Fact]
    public void ASaturatedColorKeepsItsHueAndAGreyDoesNot()
    {
        var tone = new ToneMap(Ink, Mid, Paper, 1f, 1f, 1f);
        Vector3 magenta = tone.Apply(new Vector3(0.8f, 0.1f, 0.8f));
        Assert.True(magenta.X > magenta.Y * 3f);
        Near(Mid, tone.Apply(new Vector3(ToneMap.MidValue)));
    }
}
