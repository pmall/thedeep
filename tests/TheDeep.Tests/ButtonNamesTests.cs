using Xunit;
using static TheDeep.ButtonNames;

namespace TheDeep.Tests;

public class ButtonNamesTests
{
    [Fact]
    public void AKeyOrAClickIsTheKeyboard()
    {
        Assert.Equal(Device.Keyboard, Next(Device.Gamepad, Source.Key));
        Assert.Equal(Device.Keyboard, Next(Device.Gamepad, Source.MouseButton));
    }

    [Fact]
    public void APadButtonIsTheGamepad()
    {
        Assert.Equal(Device.Gamepad, Next(Device.Keyboard, Source.PadButton));
    }

    [Fact]
    public void AStickCountsOnlyWhenPushed()
    {
        Assert.Equal(Device.Keyboard, Next(Device.Keyboard, Source.PadAxis, 0.2f));
        Assert.Equal(Device.Gamepad, Next(Device.Keyboard, Source.PadAxis, -0.8f));
    }

    [Fact]
    public void AMovedMouseChangesNothing()
    {
        Assert.Equal(Device.Gamepad, Next(Device.Gamepad, Source.MouseMotion));
    }

    [Fact]
    public void PadButtonsAreNamedAsOnAnXboxPad()
    {
        Assert.Equal(new PadGlyph("A", PadShape.Face, 0), PadButton(0));
        Assert.Equal(new PadGlyph("Y", PadShape.Face, 3), PadButton(3));
        Assert.Equal("LB", PadButton(9).Name);
        Assert.Equal(PadShape.Shoulder, PadButton(10).Shape);
        Assert.Equal(new PadGlyph("RT", PadShape.Trigger), PadAxis(5));
        Assert.Equal(PadShape.Stick, PadAxis(1).Shape);
    }

    [Fact]
    public void LongKeyNamesAreShortened()
    {
        Assert.Equal("Esc", KeyCap("Escape"));
        Assert.Equal("Space", KeyCap("Space"));
        Assert.Equal("?", KeyCap(""));
        Assert.Equal(",", KeyCap("Comma"));
    }

    [Fact]
    public void ArrowKeysPointTheirWay()
    {
        Assert.Equal(0, ArrowOf("Up"));
        Assert.Equal(3, ArrowOf("Right"));
        Assert.Equal(-1, ArrowOf("Space"));
    }
}
