namespace TheDeep;

/// <summary>
/// Pure: how a binding is named and drawn on screen, and which device the player is on. Gamepad buttons and axes are Godot's
/// SDL-style indices (JoyButton / JoyAxis), named as on an Xbox pad. The device follows the last input that meant something: a key
/// or a mouse click is the keyboard, a pad button or a stick pushed past <see cref="StickWake"/> is the gamepad; a drifting stick
/// or a moved mouse changes nothing.
/// </summary>
public static class ButtonNames
{
    /// <summary>The device whose buttons are shown.</summary>
    public enum Device { Keyboard, Gamepad }

    /// <summary>What kind of input an event was, for <see cref="Next"/>.</summary>
    public enum Source { Key, MouseButton, MouseMotion, PadButton, PadAxis }

    /// <summary>How a gamepad binding is drawn.</summary>
    public enum PadShape { Face, Shoulder, Trigger, Stick, Dpad, Small }

    /// <summary>A gamepad binding on screen: its name and its shape. <see cref="Face"/> is A, B, X, Y as 0–3 (their colors), else -1.</summary>
    public readonly record struct PadGlyph(string Name, PadShape Shape, int Face = -1);

    /// <summary>How far a stick or trigger must go before it counts as the player picking up the pad.</summary>
    public const float StickWake = 0.5f;

    /// <summary>The device after an input of <paramref name="source"/> (<paramref name="magnitude"/> for an axis).</summary>
    public static Device Next(Device current, Source source, float magnitude = 1f) => source switch
    {
        Source.Key or Source.MouseButton => Device.Keyboard,
        Source.PadButton => Device.Gamepad,
        Source.PadAxis => System.MathF.Abs(magnitude) >= StickWake ? Device.Gamepad : current,
        _ => current,
    };

    /// <summary>The gamepad button with Godot index <paramref name="index"/>.</summary>
    public static PadGlyph PadButton(int index) => index switch
    {
        0 => new("A", PadShape.Face, 0),
        1 => new("B", PadShape.Face, 1),
        2 => new("X", PadShape.Face, 2),
        3 => new("Y", PadShape.Face, 3),
        4 => new("View", PadShape.Small),
        5 => new("Home", PadShape.Small),
        6 => new("Menu", PadShape.Small),
        7 => new("L3", PadShape.Stick),
        8 => new("R3", PadShape.Stick),
        9 => new("LB", PadShape.Shoulder),
        10 => new("RB", PadShape.Shoulder),
        11 => new("Up", PadShape.Dpad),
        12 => new("Down", PadShape.Dpad),
        13 => new("Left", PadShape.Dpad),
        14 => new("Right", PadShape.Dpad),
        15 => new("Share", PadShape.Small),
        _ => new($"B{index}", PadShape.Small),
    };

    /// <summary>The gamepad axis with Godot index <paramref name="axis"/>.</summary>
    public static PadGlyph PadAxis(int axis) => axis switch
    {
        0 or 1 => new("L", PadShape.Stick),
        2 or 3 => new("R", PadShape.Stick),
        4 => new("LT", PadShape.Trigger),
        5 => new("RT", PadShape.Trigger),
        _ => new($"Axis {axis}", PadShape.Small),
    };

    /// <summary>A key's name as printed on its cap (Godot's keycode string; punctuation as its sign, long names shortened).</summary>
    public static string KeyCap(string keycodeName) => keycodeName switch
    {
        "Escape" => "Esc",
        "Backspace" => "Bksp",
        "Comma" => ",",
        "Period" => ".",
        "Semicolon" => ";",
        "Colon" => ":",
        "Slash" => "/",
        "Backslash" => "\\",
        "Apostrophe" => "'",
        "QuoteLeft" => "`",
        "Minus" => "-",
        "Equal" => "=",
        "BracketLeft" => "[",
        "BracketRight" => "]",
        "Exclam" => "!",
        "Asterisk" => "*",
        "Dollar" => "$",
        "" => "?",
        _ => keycodeName,
    };

    /// <summary>An arrow key's direction from its cap name (0 up, 1 left, 2 down, 3 right), else -1.</summary>
    public static int ArrowOf(string cap) => cap switch
    {
        "Up" => 0,
        "Left" => 1,
        "Down" => 2,
        "Right" => 3,
        _ => -1,
    };
}
