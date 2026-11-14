using System;
using Xunit;

namespace TheDeep.Tests;

public class EvenRowsTests
{
    /// <summary>Rows of <paramref name="text"/> wrapped by words at <paramref name="width"/>, every letter and space 10 pixels wide.</summary>
    private static int Rows(string text, float width)
    {
        int rows = 1;
        float row = 0f;
        foreach (string word in text.Split(' '))
        {
            float w = word.Length * 10f;
            if (row > 0f && row + 10f + w > width)
            {
                rows++;
                row = w;
            }
            else
            {
                row += (row > 0f ? 10f : 0f) + w;
            }
        }

        return rows;
    }

    private static float Even(string text, float widest) => EvenRows.Width(w => Rows(text, w), text.Length * 10f, widest);

    [Fact]
    public void ATextOnOneRowKeepsItsWidth()
    {
        Assert.Equal(110f, Even("one two six", 500f));
    }

    [Fact]
    public void ALineJustOverflowingSharesItsWordsOverTwoEvenRows()
    {
        // At 260 pixels the last word stands alone; set even, the two rows hold three words each.
        const string text = "aaaa bbbb cccc dddd eeee ffff";
        Assert.Equal(2, Rows(text, 260f));
        float width = Even(text, 260f);
        Assert.Equal(2, Rows(text, width));
        Assert.InRange(width, 140f, 150f);
    }

    [Fact]
    public void ItNeverTakesMoreRowsThanAtTheWidest()
    {
        const string text = "the makers did not come down with us and the laws walked down alone in the dark";
        foreach (float widest in new[] { 200f, 330f, 470f, 610f })
        {
            int rows = Rows(text, widest);
            float width = Even(text, widest);
            Assert.True(width <= widest);
            Assert.Equal(rows, Rows(text, width));
            Assert.True(Rows(text, width - 2f) > rows, $"at {widest}: {width} is not the narrowest");
        }
    }

    [Fact]
    public void AWordWiderThanTheBoxStillGivesAWidth()
    {
        float width = Even("unbreakable", 60f);
        Assert.False(float.IsNaN(width));
        Assert.True(width <= 60f);
    }
}
