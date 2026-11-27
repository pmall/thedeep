using Xunit;

namespace TheDeep.Tests;

public class LaunchOptionsTests
{
    private static readonly DriverFlag[] Drivers = { new("--smoke"), new("--lookdev", TakesValue: true), new("--route-check") };

    private static LaunchOptions Ok(params string[] args)
    {
        var parsed = Assert.IsType<LaunchParse.Parsed>(LaunchOptions.Parse(args, Drivers));
        return parsed.Options;
    }

    private static string Bad(params string[] args) => Assert.IsType<LaunchParse.Invalid>(LaunchOptions.Parse(args, Drivers)).Message;

    [Fact]
    public void NoArgumentsIsAnOrdinaryLaunch()
    {
        LaunchOptions options = Ok();
        Assert.Null(options.Driver);
        Assert.Null(options.Room);
        Assert.Equal((1, 1), options.Shard);
    }

    [Fact]
    public void ReadsEveryOptionOnce()
    {
        LaunchOptions options = Ok("--world", "sequel", "--room", "Quay", "--abilities", "MagGrip,drill", "--items", "A,B", "--biome", "volcanic", "--new", "--shot", "--look", "ink", "--shard", "2/4");
        Assert.Equal("sequel", options.World);
        Assert.Equal("Quay", options.Room);
        Assert.Equal(Ability.MagGrip | Ability.Drill, options.Abilities);
        Assert.Equal(new[] { "A", "B" }, options.Items);
        Assert.Equal("volcanic", options.Biome);
        Assert.True(options.NewGame);
        Assert.True(options.Shot);
        Assert.Equal("ink", options.Look);
        Assert.Equal((2, 4), options.Shard);
    }

    [Fact]
    public void ADriverTakesItsValueAndAnOptionStaysSeparate()
    {
        LaunchOptions options = Ok("--lookdev", "out", "--size", "960x540", "--perf", "p.json");
        Assert.Equal("--lookdev", options.Driver);
        Assert.Equal("out", options.DriverValue);
        Assert.Equal((960, 540), options.Size);
        Assert.Equal("p.json", options.Perf);
        Assert.Null(Ok("--smoke").DriverValue);
    }

    [Fact]
    public void ARoomValueIsNeverTheNextFlag()
    {
        Assert.Contains("--room takes a value", Bad("--room", "--abilities", "all"));
        Assert.Contains("--room takes a value", Bad("--room"));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("MagGrip,Nope")]
    [InlineData("None,MagGrip")]
    [InlineData("")]
    public void AbilitiesAreNamesNotNumbers(string list)
    {
        Assert.Contains("--abilities", Bad("--abilities", list));
    }

    [Fact]
    public void AllAndNoneNameTheExtremes()
    {
        Assert.Equal(Ability.None, Ok("--abilities", "none").Abilities);
        Ability all = Ok("--abilities", "ALL").Abilities!.Value;
        foreach (Ability a in AbilitySet.All)
        {
            Assert.True((all & a) == a);
        }
    }

    [Theory]
    [InlineData("0/4")]
    [InlineData("5/4")]
    [InlineData("x")]
    [InlineData("1/2/3")]
    [InlineData("-1/4")]
    public void AShardIsKOverNWithinRange(string shard)
    {
        Assert.Contains("--shard", Bad("--shard", shard));
    }

    [Fact]
    public void RefusesWhatItDoesNotKnow()
    {
        Assert.Contains("unknown argument --nope", Bad("--nope"));
        Assert.Contains("given twice", Bad("--room", "A", "--room", "B"));
        Assert.Contains("both run something", Bad("--smoke", "--route-check"));
        Assert.Contains("--size", Bad("--size", "big"));
        Assert.Contains("--hops", Bad("--hops", "many"));
    }
}
