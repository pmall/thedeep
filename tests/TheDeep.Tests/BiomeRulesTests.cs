using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class BiomeRulesTests
{
    private static readonly BiomePalette Good = new()
    {
        Rock = new Vector3(0.3f, 0.3f, 0.32f),
        Metal = new Vector3(0.6f, 0.64f, 0.7f),
        Rust = new Vector3(0.4f, 0.32f, 0.28f),
        MoltenCool = new Vector3(0.5f, 0.06f, 0.02f),
        Molten = new Vector3(1f, 0.4f, 0.08f),
        MoltenHot = new Vector3(1f, 0.85f, 0.55f),
        Light = [("FogColor", new Vector3(0.2f, 0.18f, 0.17f)), ("LampColor", new Vector3(1f, 0.8f, 0.6f))],
        Shares = [("Frost", 0f), ("Vignette", 0.7f)],
    };

    [Fact]
    public void AKeptPaletteHasNoProblem() => Assert.Empty(BiomeRules.Problems(Good));

    [Fact]
    public void MetalTooCloseToRockInValueIsAProblem()
    {
        string problem = Assert.Single(BiomeRules.Problems(Good with { Metal = new Vector3(0.38f, 0.4f, 0.44f) }));
        Assert.Contains("metal", problem);
    }

    [Fact]
    public void SaturatedRustIsAProblem() => Assert.Single(BiomeRules.Problems(Good with { Rust = new Vector3(0.6f, 0.2f, 0.1f) }));

    [Fact]
    public void MoltenColorsOutOfOrderAreAProblem() => Assert.Single(BiomeRules.Problems(Good with { MoltenHot = new Vector3(0.6f, 0.2f, 0.05f) }));

    [Fact]
    public void LightAsSaturatedAsTheHazardIsAProblemNamingIt()
    {
        BiomePalette washed = Good with { Light = [.. Good.Light, ("HazeColor", new Vector3(0.7f, 0.12f, 0.06f))] };
        Assert.Contains("HazeColor", Assert.Single(BiomeRules.Problems(washed)));
    }

    [Fact]
    public void AShareOutsideZeroToOneIsAProblem() => Assert.Single(BiomeRules.Problems(Good with { Shares = [("Frost", 1.2f)] }));

    [Fact]
    public void ValueWeighsGreenMostAndChromaIsTheSpreadOfChannels()
    {
        Assert.True(BiomeRules.Value(Vector3.UnitY) > BiomeRules.Value(Vector3.UnitX));
        Assert.Equal(1f, BiomeRules.Value(Vector3.One), 4);
        Assert.Equal(0f, BiomeRules.Chroma(new Vector3(0.4f, 0.4f, 0.4f)));
        Assert.Equal(1f, BiomeRules.Chroma(Vector3.UnitX));
    }
}
