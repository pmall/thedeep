using System.Linq;
using TheDeep;
using Xunit;

namespace TheDeep.Tests;

public class OverlayTests
{
    private static string? Key((string? Key, int Value) e) => e.Key;

    [Fact]
    public void AKnownKeyReplacesInPlaceAndOthersAreAppended()
    {
        var merged = Overlay.Merge(new[] { ("a", 1), ("b", 2), ("c", 3) }, new[] { ("b", 20), ("d", 4) }, Key);

        Assert.Equal(new[] { ("a", 1), ("b", 20), ("c", 3), ("d", 4) }, merged);
    }

    [Fact]
    public void EntriesWithoutAKeyAreAlwaysAppended()
    {
        var merged = Overlay.Merge(new[] { ((string?)null, 1), ("", 2) }, new[] { ((string?)null, 3), ("", 4) }, Key);

        Assert.Equal(new[] { 1, 2, 3, 4 }, merged.Select(e => e.Item2));
    }

    [Fact]
    public void TheInheritedListIsLeftAlone()
    {
        var inherited = new[] { ("a", 1) };

        Overlay.Merge(inherited, new[] { ("a", 2) }, Key);

        Assert.Equal(1, inherited[0].Item2);
    }
}
