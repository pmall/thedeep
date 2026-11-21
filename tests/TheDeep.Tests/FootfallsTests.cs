using Xunit;

namespace TheDeep.Tests;

public class FootfallsTests
{
    [Fact]
    public void AFootPlantsOnceEachTimeItComesDown()
    {
        var feet = new Footfalls();
        Assert.Equal(0, feet.Update(0f, 0f, walking: true)); // standing: nothing lifted yet
        Assert.Equal(0, feet.Update(0.2f, 0f, walking: true));
        Assert.Equal(1, feet.Update(0.01f, 0.1f, walking: true));
        Assert.Equal(0, feet.Update(0.0f, 0.2f, walking: true)); // still down: no second footfall
        Assert.Equal(-1, feet.Update(0.1f, 0.02f, walking: true));
    }

    [Fact]
    public void AFootRollingInPlaceNeverPlants()
    {
        var feet = new Footfalls();
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(0, feet.Update(i % 2 == 0 ? 0f : 0.06f, 0f, walking: true));
        }
    }

    [Fact]
    public void OffTheGroundNothingPlants()
    {
        var feet = new Footfalls();
        feet.Update(0.3f, 0.3f, walking: true);
        Assert.Equal(0, feet.Update(0f, 0f, walking: false));
        Assert.Equal(0, feet.Update(0f, 0f, walking: true));
    }
}
