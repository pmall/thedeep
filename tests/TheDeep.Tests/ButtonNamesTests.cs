using Xunit;
using static TheDeep.ButtonNames;

namespace TheDeep.Tests;

public class ButtonNamesTests
{
    [Fact]
    public void A_key_or_a_click_is_the_keyboard()
    {
        Assert.Equal(Device.Keyboard, Next(Device.Gamepad, Source.Key));
        Assert.Equal(Device.Keyboard, Next(Device.Gamepad, Source.MouseButton));
    }

    [Fact]
    public void A_pad_button_is_the_gamepad()
    {
        Assert.Equal(Device.Gamepad, Next(Device.Keyboard, Source.PadButton));
    }

    [Fact]
    public void A_stick_counts_only_when_pushed()
    {
        Assert.Equal(Device.Keyboard, Next(Device.Keyboard, Source.PadAxis, 0.2f));
        Assert.Equal(Device.Gamepad, Next(Device.Keyboard, Source.PadAxis, -0.8f));
    }

    [Fact]
    public void A_moved_mouse_changes_nothing()
    {
        Assert.Equal(Device.Gamepad, Next(Device.Gamepad, Source.MouseMotion));
    }

    [Fact]
    public void Pad_buttons_are_named_as_on_an_xbox_pad()
    {
        Assert.Equal(new PadGlyph("A", PadShape.Face, 0), PadButton(0));
        Assert.Equal(new PadGlyph("Y", PadShape.Face, 3), PadButton(3));
        Assert.Equal("LB", PadButton(9).Name);
        Assert.Equal(PadShape.Shoulder, PadButton(10).Shape);
        Assert.Equal(new PadGlyph("RT", PadShape.Trigger), PadAxis(5));
        Assert.Equal(PadShape.Stick, PadAxis(1).Shape);
    }

    [Fact]
    public void Long_key_names_are_shortened()
    {
        Assert.Equal("Esc", KeyCap("Escape"));
        Assert.Equal("Space", KeyCap("Space"));
        Assert.Equal("?", KeyCap(""));
        Assert.Equal(",", KeyCap("Comma"));
    }

    [Fact]
    public void Arrow_keys_point_their_way()
    {
        Assert.Equal(0, ArrowOf("Up"));
        Assert.Equal(3, ArrowOf("Right"));
        Assert.Equal(-1, ArrowOf("Space"));
    }
}
