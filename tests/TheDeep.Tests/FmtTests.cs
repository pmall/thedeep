using System.Globalization;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class FmtTests
{
    [Fact]
    public void NumbersAndPlacesUseAPointWhateverTheCulture()
    {
        CultureInfo kept = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.Equal("1.5", Fmt.Num(1.5f));
            Assert.Equal("(1.5, -2)", Fmt.Point(new Vector2(1.5f, -2f)));
            Assert.Equal("(0, 0.25)..(3.5, 4)", Fmt.Rect(new Box(0f, 0.25f, 3.5f, 4f)));
        }
        finally
        {
            CultureInfo.CurrentCulture = kept;
        }
    }
}
