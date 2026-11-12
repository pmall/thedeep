using Godot;

namespace TheDeep;

/// <summary>
/// One button as the player sees it: a key cap or a pad glyph, read from the input map (keys by the layout's own labels: on AZERTY
/// the physical WASD reads Z Q S D), lit while any of its actions is held. Drawn by a <see cref="ButtonPainter"/>; shared by the
/// <see cref="ControlsManual"/> and the <see cref="ActionPrompt"/>, so a button reads the same wherever it is shown.
/// </summary>
public sealed class ButtonGlyph
{
    /// <summary>The key's or button's name, written on its cap.</summary>
    public string Text { get; init; } = "";
    /// <summary>The pad button's shape, for a pad glyph.</summary>
    public ButtonNames.PadShape? Shape { get; init; }
    /// <summary>Which face button it is, A, B, X, Y as 0–3 (their colors); -1 for any other.</summary>
    public int Face { get; init; } = -1;
    /// <summary>The input actions that light it while held.</summary>
    public StringName[] Actions { get; set; } = System.Array.Empty<StringName>();
    /// <summary>How lit it is now, 0–1.</summary>
    public float Lit { get; set; }
    /// <summary>A stick pushed one way (a direction on the stick), or zero for the live stick.</summary>
    public Vector2 Push { get; set; }

    /// <summary>The glyph of binding <paramref name="e"/> of <paramref name="action"/> on the pad or the keyboard; none if it is the other device's.</summary>
    public static ButtonGlyph? Of(InputEvent e, bool pad, StringName action)
    {
        switch (e)
        {
            case InputEventKey key when !pad:
            {
                // The layout's label for the physical key (headless has no layout: the physical key's US name).
                Key code = key.PhysicalKeycode == Key.None ? key.Keycode
                    : DisplayServer.GetName() == "headless" ? key.PhysicalKeycode
                    : DisplayServer.KeyboardGetKeycodeFromPhysical(key.PhysicalKeycode);
                return new ButtonGlyph { Text = ButtonNames.KeyCap(OS.GetKeycodeString(code)), Actions = new[] { action } };
            }
            case InputEventJoypadButton button when pad:
                return Of(ButtonNames.PadButton((int)button.ButtonIndex), action);
            case InputEventJoypadMotion motion when pad:
            {
                ButtonGlyph glyph = Of(ButtonNames.PadAxis((int)motion.Axis), action);
                if (glyph.Shape == ButtonNames.PadShape.Stick)
                {
                    float sign = Mathf.Sign(motion.AxisValue);
                    glyph.Push = (int)motion.Axis % 2 == 0 ? new Vector2(sign, 0f) : new Vector2(0f, sign);
                }

                return glyph;
            }
            default:
                return null;
        }
    }

    /// <summary>The main button of <paramref name="action"/> on <paramref name="device"/>: its first binding there; none if it has none.</summary>
    public static ButtonGlyph? Main(StringName action, ButtonNames.Device device)
    {
        foreach (InputEvent e in InputMap.ActionGetEvents(action))
        {
            if (Of(e, device == ButtonNames.Device.Gamepad, action) is { } glyph)
            {
                return glyph;
            }
        }

        return null;
    }

    /// <summary>The device the player starts on: the pad when one is connected.</summary>
    public static ButtonNames.Device FirstDevice() =>
        Input.GetConnectedJoypads().Count > 0 ? ButtonNames.Device.Gamepad : ButtonNames.Device.Keyboard;

    /// <summary>The device after <paramref name="e"/> (<see cref="ButtonNames.Next"/>).</summary>
    public static ButtonNames.Device DeviceAfter(ButtonNames.Device current, InputEvent e) => e switch
    {
        InputEventKey { Pressed: true } => ButtonNames.Next(current, ButtonNames.Source.Key),
        InputEventMouseButton { Pressed: true } => ButtonNames.Next(current, ButtonNames.Source.MouseButton),
        InputEventJoypadButton { Pressed: true } => ButtonNames.Next(current, ButtonNames.Source.PadButton),
        InputEventJoypadMotion motion => ButtonNames.Next(current, ButtonNames.Source.PadAxis, motion.AxisValue),
        _ => current,
    };

    /// <summary>Lights while any of its actions is held: at once, fading after release back to <paramref name="rest"/>.</summary>
    public void Light(float dt, float rest = 0f)
    {
        bool held = false;
        foreach (StringName action in Actions)
        {
            held |= Input.IsActionPressed(action);
        }

        Lit = held ? Motion.Approach(Lit, 1f, dt * 24f) : Motion.Approach(Lit, rest, dt * 5f);
    }

    private static ButtonGlyph Of(ButtonNames.PadGlyph glyph, StringName action) =>
        new() { Text = glyph.Name, Shape = glyph.Shape, Face = glyph.Face, Actions = new[] { action } };
}
