using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class DressingLayoutTests
{
    [Fact]
    public void HashIsStableAndInRange()
    {
        for (int i = 0; i < 1000; i++)
        {
            float h = DressingLayout.Hash01(7, i);
            Assert.InRange(h, 0f, 0.99999f);
            Assert.Equal(h, DressingLayout.Hash01(7, i));
        }

        Assert.NotEqual(DressingLayout.Hash01(1, 5), DressingLayout.Hash01(2, 5));
    }

    [Fact]
    public void HashIsRoughlyUniform()
    {
        float mean = Enumerable.Range(0, 4000).Select(i => DressingLayout.Hash01(3, i)).Average();
        Assert.InRange(mean, 0.47f, 0.53f);
    }

    [Fact]
    public void ChosenFollowsTheChance()
    {
        Assert.Empty(DressingLayout.Chosen(50, 0f, 1));
        Assert.Equal(Enumerable.Range(0, 50), DressingLayout.Chosen(50, 1f, 1));
        int some = DressingLayout.Chosen(400, 0.25f, 9).Count();
        Assert.InRange(some, 70, 130);
    }

    [Fact]
    public void RowIsDeterministic()
    {
        var a = DressingLayout.Row(40, 3, 0.2f, 0.1f, 5);
        var b = DressingLayout.Row(40, 3, 0.2f, 0.1f, 5);
        Assert.Equal(a, b);
        Assert.NotEqual(a, DressingLayout.Row(40, 3, 0.2f, 0.1f, 6));
    }

    [Fact]
    public void RowNeverRepeatsAVariantOrToppleSideBySide()
    {
        var slots = DressingLayout.Row(200, 3, 0.1f, 0.5f, 2);
        for (int i = 1; i < slots.Count; i++)
        {
            if (slots[i].Index == slots[i - 1].Index + 1)
            {
                Assert.NotEqual(slots[i - 1].Variant, slots[i].Variant);
                Assert.False(slots[i - 1].Toppled && slots[i].Toppled);
            }
        }

        Assert.All(slots, s => Assert.InRange(s.Variant, 0, 2));
    }

    [Fact]
    public void RowWithoutGapsFillsEverySlot()
    {
        var slots = DressingLayout.Row(12, 1, 0f, 0f, 4);
        Assert.Equal(Enumerable.Range(0, 12), slots.Select(s => s.Index));
        Assert.All(slots, s => Assert.Equal(0, s.Variant));
    }

    [Theory]
    [InlineData(8f, true)]
    [InlineData(9.5f, true)]
    [InlineData(10.5f, false)]
    [InlineData(-0.5f, true)]
    [InlineData(-2.5f, false)]
    [InlineData(4f, false)]
    public void NearRepeatFindsThePiers(float x, bool near)
    {
        Assert.Equal(near, DressingLayout.NearRepeat(x, 0f, 8f, 1.5f));
    }
}
