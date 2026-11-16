using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class BiomeLightTests
{
    [Fact]
    public void AFullShareLeavesTheShaftColorAsItIs()
    {
        var color = new Vector3(0.8f, 0.5f, 0.1f);
        Vector3 tint = BiomeLight.ShaftTint(color, 1f);
        Assert.Equal(color.X, tint.X, 3);
        Assert.Equal(color.Y, tint.Y, 3);
        Assert.Equal(color.Z, tint.Z, 3);
    }

    [Fact]
    public void ALesserShareDimsInLinearLight()
    {
        // Half of white's linear light is 0.5, which sRGB writes as about 0.735.
        Vector3 tint = BiomeLight.ShaftTint(Vector3.One, 0.5f);
        Assert.Equal(0.735f, tint.X, 3);
        Assert.Equal(Vector3.Zero, BiomeLight.ShaftTint(Vector3.One, 0f));
    }

    [Fact]
    public void WhitenGoesStraightTowardWhite()
    {
        Assert.Equal(new Vector3(0.5f, 0.6f, 1f), BiomeLight.Whiten(new Vector3(0f, 0.2f, 1f), 0.5f));
        Assert.Equal(new Vector3(0.2f), BiomeLight.Whiten(new Vector3(0.2f), 0f));
    }
}
