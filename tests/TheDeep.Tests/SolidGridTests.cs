using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class SolidGridTests
{
    private static readonly Box[] Solids =
    {
        new(-20f, -1f, 20f, 0f),  // a long floor
        new(3f, 0f, 4f, 6f),      // a pillar
        new(10f, 4f, 14f, 5f),    // a shelf
        new(-30f, -5f, -29f, 30f), // a far wall
    };

    [Fact]
    public void NearHoldsEverySolidThatOverlaps()
    {
        var grid = new SolidGrid(Solids);
        var near = new List<int>();
        var random = new Random(7);
        for (int n = 0; n < 500; n++)
        {
            float x = (float)(random.NextDouble() * 70 - 35), y = (float)(random.NextDouble() * 40 - 8);
            var area = new Box(x, y, x + (float)random.NextDouble() * 3f, y + (float)random.NextDouble() * 3f);
            grid.Near(area, near);
            for (int i = 0; i < Solids.Length; i++)
            {
                if (Solids[i].Overlaps(area))
                {
                    Assert.Contains(i, near);
                }
            }
        }
    }

    [Fact]
    public void NearIsAscendingAndEachOnce()
    {
        var grid = new SolidGrid(Solids);
        var near = new List<int>();

        grid.Near(new Box(2f, -0.5f, 12f, 4.5f), near); // the floor, the pillar and the shelf, over many cells

        Assert.Equal(new[] { 0, 1, 2 }, near);
    }

    [Fact]
    public void FarFromEverySolidHoldsNoneOfThem()
    {
        var grid = new SolidGrid(Solids);
        var near = new List<int> { 99 };

        grid.Near(new Box(-10f, 20f, -9f, 21f), near);

        Assert.Empty(near);
    }

    [Fact]
    public void NoSolidsIsEmpty()
    {
        var grid = new SolidGrid(Array.Empty<Box>());
        var near = new List<int>();

        grid.Near(new Box(0f, 0f, 1f, 1f), near);

        Assert.Empty(near.ToList());
    }
}
