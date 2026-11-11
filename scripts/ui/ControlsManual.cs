using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Debug builds: a small manual on screen, each button lit while held. Two of them (DebugHud): the gameplay manual, bottom left
/// (<see cref="Gameplay"/>: each action's main button and what it does; keyboard caps while the player is on the keyboard, gamepad
/// buttons while on the pad, <see cref="ButtonNames.Next"/>, crossfading when the device changes; F8 hides it), and the debug keys,
/// top left with the F7 readout (<see cref="Debug"/>: the keyboard key, then the pad button where there is one). Buttons are read
/// from the input map, keys by the layout's own labels (on AZERTY the physical WASD reads Z Q S D), so the manual never drifts
/// from the bindings. An ability's rows are faint until it is held. Words come from the line book.
/// </summary>
public partial class ControlsManual : Control
{
    /// <summary>The pilgrim whose abilities dim the rows it can't use yet.</summary>
    public required Player Player { get; init; }

    /// <summary>The device shown; set by the last input, or forced (UiShot).</summary>
    public ButtonNames.Device Device { get; set; }

    private const float Pad = 10f;
    private const float RowHeight = 21f;
    private const float HeaderHeight = 22f;
    private const float CapHeight = 18f;
    private const float CapGap = 4f;
    private const float Column = 10f;
    private const int CapFont = 12;
    private const int WordFont = 12;
    private const int HeaderFont = 11;

    private static readonly Color Ink = new(0.78f, 0.86f, 0.88f);
    private static readonly Color Dark = new(0.04f, 0.05f, 0.07f);
    private static readonly Color[] FaceColors =
    {
        new(0.47f, 0.82f, 0.52f), new(0.92f, 0.46f, 0.42f), new(0.48f, 0.62f, 0.96f), new(0.95f, 0.82f, 0.42f),
    };

    /// <summary>How a row's actions become buttons.</summary>
    public enum Kind
    {
        /// <summary>Every binding of the one action, "/" between.</summary>
        Any,
        /// <summary>The first binding of each action, joined by the row's separator.</summary>
        Join,
        /// <summary>Movement: one group per binding (WASD / arrows; stick / d-pad).</summary>
        Set,
    }

    /// <summary>One row of the manual: what the pilgrim can do and the buttons that do it.</summary>
    /// <param name="Line">The line naming the action.</param>
    /// <param name="Kind">How <paramref name="Actions"/> become buttons.</param>
    /// <param name="Actions">The input actions whose bindings the row shows.</param>
    /// <param name="Needs">The ability the row is for: the row is dimmed until the pilgrim owns it.</param>
    /// <param name="Separator">What stands between the buttons of a joined row.</param>
    public sealed record Spec(StringName Line, Kind Kind, StringName[] Actions, Ability Needs = Ability.None, string Separator = "/");

    private static readonly StringName[] MoveSet = { InputActions.MoveUp, InputActions.MoveLeft, InputActions.MoveDown, InputActions.MoveRight };

    /// <summary>The gameplay manual: the main button of each action, keyboard or gamepad.</summary>
    public static readonly Spec[] Gameplay =
    {
        new("ui.manual.move", Kind.Set, MoveSet),
        new("ui.manual.jump", Kind.Any, new[] { InputActions.Jump }),
        new("ui.manual.dash", Kind.Any, new[] { InputActions.Dash }),
        new("ui.manual.attack", Kind.Any, new[] { InputActions.Attack }),
        new("ui.manual.harpoon", Kind.Any, new[] { InputActions.Harpoon }, Ability.Harpoon),
        new("ui.manual.drill", Kind.Join, new[] { InputActions.MoveDown, InputActions.Dash }, Ability.Drill, "+"),
        new("ui.manual.ballast", Kind.Join, new[] { InputActions.MoveDown, InputActions.MoveUp }, Ability.Ballast),
        new("ui.manual.mag_grip", Kind.Join, new[] { InputActions.MoveLeft, InputActions.MoveRight }, Ability.MagGrip),
        new("ui.manual.interact", Kind.Any, new[] { InputActions.Interact }),
        new("ui.manual.map", Kind.Any, new[] { InputActions.Map }),
    };

    /// <summary>The debug keys (keyboard only), shown with the F7 readout.</summary>
    public static readonly Spec[] Debug =
    {
        new("ui.manual.debug", Kind.Any, System.Array.Empty<StringName>()),
        new("ui.manual.respawn", Kind.Any, new[] { InputActions.DebugRespawn }),
        new("ui.manual.toggle_abilities", Kind.Join, new[]
        {
            InputActions.DebugToggleMagGrip, InputActions.DebugToggleHarpoon, InputActions.DebugToggleDrill,
            InputActions.DebugToggleBallast, InputActions.DebugToggleHeatPlating,
        }, Ability.None, ""),
        new("ui.manual.biome", Kind.Any, new[] { InputActions.DebugCycleBiome }),
        new("ui.manual.readout", Kind.Any, new[] { InputActions.DebugToggleHud }),
        new("ui.manual.hide", Kind.Any, new[] { InputActions.DebugToggleManual }),
        new("ui.manual.screenshot", Kind.Any, new[] { InputActions.CaptureScreenshot }),
        new("ui.manual.video", Kind.Any, new[] { InputActions.CaptureVideo }),
    };

    /// <summary>The rows shown (<see cref="Gameplay"/> or <see cref="Debug"/>).</summary>
    public Spec[] Specs { get; init; } = Gameplay;

    /// <summary>The keyboard's key and the pad's button side by side, whatever the device (the debug keys).</summary>
    public bool BothDevices { get; init; }

    /// <summary>Sits at the top left, <see cref="TopOffset"/> down; otherwise at the bottom left.</summary>
    public bool Top { get; init; }

    public float TopOffset { get; init; } = 150f;

    /// <summary>What a row shows for one device, left to right: buttons (<see cref="Glyph"/>) and the marks between them (<see cref="Separator"/>).</summary>
    private abstract class Item
    {
        /// <summary>Its width on screen, measured once the row is built.</summary>
        public float Width;
    }

    /// <summary>A mark between buttons: "/" between alternatives, "+" between buttons pressed together.</summary>
    private sealed class Separator(string text) : Item
    {
        public string Text { get; } = text;
    }

    /// <summary>One button on screen: a key cap, or a pad glyph; lit while any of its actions is held.</summary>
    private sealed class Glyph : Item
    {
        public string Text = "";
        public ButtonNames.PadShape? Shape;
        public int Face = -1;
        public StringName[] Actions = System.Array.Empty<StringName>();
        public float Lit;

        /// <summary>A stick pushed one way (a direction on the stick), or zero for the live stick.</summary>
        public Vector2 Push;
    }

    /// <summary>A row as drawn: its spec, its words, and for each device its buttons and the separators between them.</summary>
    private sealed class Row(Spec spec, string words, bool header)
    {
        public Spec Spec { get; } = spec;
        public string Words { get; } = words;
        public bool Header { get; } = header;
        public readonly List<Item>[] Items = { new(), new() };
    }

    private readonly List<Row> _rows = new();
    private ButtonNames.Device _shown;
    private float _alpha = 1f;
    private Vector2 _stick;
    private readonly StyleBoxFlat _cap = new() { AntiAliasing = true, CornerDetail = 5 };
    private readonly StyleBoxFlat _panel = new() { AntiAliasing = true, CornerDetail = 8 };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _cap.SetCornerRadiusAll(5);
        _cap.SetBorderWidthAll(1);
        _panel.SetCornerRadiusAll(10);
        _panel.BgColor = Dark with { A = 0.62f };

        Font font = GetThemeDefaultFont();
        foreach (Spec spec in Specs)
        {
            var row = new Row(spec, LineBook.Shared.Find(spec.Line)?.Text ?? "", header: spec.Actions.Length == 0 && spec.Needs == Ability.None);
            List<Item> keys = row.Items[(int)ButtonNames.Device.Keyboard];
            List<Item> buttons = row.Items[(int)ButtonNames.Device.Gamepad];
            Build(keys, spec, false, font);
            Build(buttons, spec, true, font);
            if (BothDevices && buttons.Count > 0)
            {
                keys.Add(new Separator("/") { Width = Width(font, "/", CapFont) });
                keys.AddRange(buttons);
            }

            _rows.Add(row);
        }

        Device = _shown = Input.GetConnectedJoypads().Count > 0 && !BothDevices ? ButtonNames.Device.Gamepad : ButtonNames.Device.Keyboard;
    }

    public override void _Input(InputEvent @event)
    {
        if (BothDevices)
        {
            return;
        }

        Device = @event switch
        {
            InputEventKey { Pressed: true } => ButtonNames.Next(Device, ButtonNames.Source.Key),
            InputEventMouseButton { Pressed: true } => ButtonNames.Next(Device, ButtonNames.Source.MouseButton),
            InputEventJoypadButton { Pressed: true } => ButtonNames.Next(Device, ButtonNames.Source.PadButton),
            InputEventJoypadMotion motion => ButtonNames.Next(Device, ButtonNames.Source.PadAxis, motion.AxisValue),
            _ => Device,
        };
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
        {
            return;
        }

        float dt = (float)delta;
        _alpha = Motion.Approach(_alpha, Device == _shown ? 1f : 0f, dt * 8f);
        if (_alpha <= 0f)
        {
            _shown = Device;
        }

        foreach (Row row in _rows)
        {
            foreach (Item item in row.Items[(int)_shown])
            {
                if (item is Glyph glyph)
                {
                    bool held = false;
                    foreach (StringName action in glyph.Actions)
                    {
                        held |= Input.IsActionPressed(action);
                    }

                    // Lit at once, fading after release.
                    glyph.Lit = held ? Motion.Approach(glyph.Lit, 1f, dt * 24f) : Motion.Approach(glyph.Lit, 0f, dt * 5f);
                }
            }
        }

        Vector2 stick = Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);
        _stick = _stick.Lerp(stick, 1f - Mathf.Exp(-dt * 30f));
        QueueRedraw();
    }

    public override void _Draw()
    {
        Font font = GetThemeDefaultFont();
        int device = (int)_shown;
        float a = _alpha * _alpha * (3f - 2f * _alpha);

        float buttons = 0f;
        float words = 0f;
        float height = 0f;
        foreach (Row row in _rows)
        {
            buttons = Mathf.Max(buttons, ItemsWidth(row.Items[device]));
            words = Mathf.Max(words, Width(font, row.Words, row.Header ? HeaderFont : WordFont));
            height += row.Header ? HeaderHeight : RowHeight;
        }

        Vector2 size = new(Pad * 2f + buttons + Column + words, Pad * 2f + height - 4f);
        Vector2 origin = new(16f, Top ? TopOffset : Size.Y - 16f - size.Y);
        DrawStyleBox(_panel, new Rect2(origin, size));

        float y = origin.Y + Pad;
        float wordsX = origin.X + Pad + buttons + Column;
        foreach (Row row in _rows)
        {
            if (row.Header)
            {
                y += 4f;
                DrawString(font, new Vector2(origin.X + Pad, y + 12f), row.Words.ToUpperInvariant(), HorizontalAlignment.Left, -1f, HeaderFont,
                    Ink with { A = 0.45f * a });
                DrawLine(new Vector2(wordsX, y + 8f), new Vector2(origin.X + size.X - Pad, y + 8f), Ink with { A = 0.12f * a }, 1f, true);
                y += HeaderHeight - 4f;
                continue;
            }

            float rowA = a * (row.Spec.Needs == Ability.None || Player.Abilities.Has(row.Spec.Needs) ? 1f : 0.5f);
            float mid = y + RowHeight * 0.5f - 1f;
            float x = origin.X + Pad;
            List<Item> items = row.Items[device];
            if (items.Count == 0)
            {
                DrawString(font, new Vector2(x, mid + 4.5f), "—", HorizontalAlignment.Left, -1f, CapFont, Ink with { A = 0.35f * rowA });
            }

            foreach (Item item in items)
            {
                switch (item)
                {
                    case Glyph glyph:
                        DrawGlyph(font, glyph, new Vector2(x + glyph.Width * 0.5f, mid + 1.5f * glyph.Lit), rowA);
                        break;
                    case Separator separator:
                        DrawString(font, new Vector2(x, mid + 4.5f), separator.Text, HorizontalAlignment.Left, -1f, CapFont, Ink with { A = 0.5f * rowA });
                        break;
                }

                x += item.Width + CapGap;
            }

            DrawString(font, new Vector2(wordsX, mid + 5f), row.Words, HorizontalAlignment.Left, -1f, WordFont, Ink with { A = 0.85f * rowA });
            y += RowHeight;
        }
    }

    // ---- building the rows ----

    private static void Build(List<Item> items, Spec spec, bool pad, Font font)
    {
        switch (spec.Kind)
        {
            case Kind.Any when spec.Actions.Length == 1:
                // The main button only (the first bound for the device): the manual stays small.
                foreach (InputEvent e in InputMap.ActionGetEvents(spec.Actions[0]))
                {
                    if (GlyphOf(e, pad, spec.Actions[0]) is { } glyph)
                    {
                        Add(items, glyph, "/");
                        break;
                    }
                }

                break;
            case Kind.Join:
                foreach (StringName action in spec.Actions)
                {
                    foreach (InputEvent e in InputMap.ActionGetEvents(action))
                    {
                        if (GlyphOf(e, pad, action) is { } glyph)
                        {
                            Add(items, glyph, spec.Separator);
                            break;
                        }
                    }
                }

                break;
            case Kind.Set:
                BuildSet(items, spec.Actions, pad);
                break;
        }

        foreach (Item item in items)
        {
            switch (item)
            {
                case Glyph glyph:
                    glyph.Width = GlyphWidth(font, glyph);
                    break;
                case Separator separator:
                    separator.Width = Width(font, separator.Text, CapFont);
                    break;
            }
        }
    }

    /// <summary>Movement: the n-th binding of each direction makes a group (W A S D, then the arrows); a stick or a d-pad is one glyph.</summary>
    private static void BuildSet(List<Item> items, StringName[] actions, bool pad)
    {
        var events = new Godot.Collections.Array<InputEvent>[actions.Length];
        int count = 0;
        for (int i = 0; i < actions.Length; i++)
        {
            events[i] = InputMap.ActionGetEvents(actions[i]);
            count = Mathf.Max(count, events[i].Count);
        }

        // The first group only (W A S D; the left stick): the manual stays small.
        for (int n = 0; n < count && items.Count == 0; n++)
        {
            Glyph? whole = null;
            var group = new List<Glyph>();
            for (int i = 0; i < actions.Length; i++)
            {
                if (n >= events[i].Count || GlyphOf(events[i][n], pad, actions[i]) is not { } glyph)
                {
                    continue;
                }

                if (glyph.Shape is ButtonNames.PadShape.Stick or ButtonNames.PadShape.Dpad)
                {
                    if (whole is null)
                    {
                        whole = glyph;
                        whole.Push = Vector2.Zero;
                    }
                    else
                    {
                        whole.Actions = [.. whole.Actions, actions[i]];
                    }
                }
                else
                {
                    group.Add(glyph);
                }
            }

            Add(items, whole, "/");
            for (int g = 0; g < group.Count; g++)
            {
                Add(items, group[g], g == 0 ? "/" : "");
            }
        }
    }

    private static void Add(List<Item> items, Glyph? glyph, string separator)
    {
        if (glyph is null)
        {
            return;
        }

        if (items.Count > 0 && separator != "")
        {
            items.Add(new Separator(separator));
        }

        items.Add(glyph);
    }

    private static Glyph? GlyphOf(InputEvent e, bool pad, StringName action)
    {
        switch (e)
        {
            case InputEventKey key when !pad:
            {
                // The layout's label for the physical key (headless has no layout: the physical key's US name).
                Key code = key.PhysicalKeycode == Key.None ? key.Keycode
                    : DisplayServer.GetName() == "headless" ? key.PhysicalKeycode
                    : DisplayServer.KeyboardGetKeycodeFromPhysical(key.PhysicalKeycode);
                return new Glyph { Text = ButtonNames.KeyCap(OS.GetKeycodeString(code)), Actions = new[] { action } };
            }
            case InputEventJoypadButton button when pad:
                return Of(ButtonNames.PadButton((int)button.ButtonIndex), action);
            case InputEventJoypadMotion motion when pad:
            {
                Glyph glyph = Of(ButtonNames.PadAxis((int)motion.Axis), action);
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

    private static Glyph Of(ButtonNames.PadGlyph glyph, StringName action) =>
        new() { Text = glyph.Name, Shape = glyph.Shape, Face = glyph.Face, Actions = new[] { action } };

    // ---- drawing ----

    private static float GlyphWidth(Font font, Glyph glyph) => glyph.Shape switch
    {
        ButtonNames.PadShape.Face or ButtonNames.PadShape.Stick or ButtonNames.PadShape.Dpad => CapHeight + 2f,
        ButtonNames.PadShape.Shoulder or ButtonNames.PadShape.Trigger => 34f,
        _ when ButtonNames.ArrowOf(glyph.Text) >= 0 => CapHeight + 3f,
        _ => Mathf.Max(CapHeight + 3f, Width(font, glyph.Text, CapFont) + 12f),
    };

    private static float ItemsWidth(List<Item> items)
    {
        float w = 0f;
        foreach (Item item in items)
        {
            w += item.Width + CapGap;
        }

        return Mathf.Max(0f, w - CapGap);
    }

    private void DrawGlyph(Font font, Glyph glyph, Vector2 mid, float a)
    {
        float lit = glyph.Lit;
        switch (glyph.Shape)
        {
            case ButtonNames.PadShape.Face:
            {
                Color tint = FaceColors[glyph.Face];
                float r = CapHeight * 0.5f + 1f;
                DrawCircle(mid, r, Dark.Lerp(tint, lit * 0.9f) with { A = (0.5f + 0.4f * lit) * a });
                DrawArc(mid, r, 0f, Mathf.Tau, 32, tint with { A = (0.7f + 0.3f * lit) * a }, 1.2f, true);
                DrawString(font, new Vector2(mid.X - r, mid.Y + 4.5f), glyph.Text, HorizontalAlignment.Center, 2f * r, CapFont,
                    tint.Lerp(Dark, lit) with { A = a });
                break;
            }
            case ButtonNames.PadShape.Stick:
            {
                float r = CapHeight * 0.5f + 1f;
                DrawCircle(mid, r, Dark with { A = 0.5f * a });
                DrawArc(mid, r, 0f, Mathf.Tau, 32, Ink with { A = (0.55f + 0.4f * lit) * a }, 1.2f, true);
                Vector2 knob = mid + (glyph.Push == Vector2.Zero ? _stick : glyph.Push) * 4f;
                DrawCircle(knob, r - 4.5f, Ink with { A = (0.18f + 0.7f * lit) * a });
                DrawString(font, new Vector2(knob.X - r, knob.Y + 4f), glyph.Text, HorizontalAlignment.Center, 2f * r, 11,
                    Ink.Lerp(Dark, lit) with { A = 0.9f * a });
                break;
            }
            case ButtonNames.PadShape.Dpad:
            {
                // A cross; each arm lights with its direction.
                const float arm = 5.5f;
                const float half = 3.2f;
                DrawRect(new Rect2(mid - new Vector2(half, arm + half), new Vector2(2f * half, 2f * (arm + half))), Dark with { A = 0.5f * a });
                DrawRect(new Rect2(mid - new Vector2(arm + half, half), new Vector2(2f * (arm + half), 2f * half)), Dark with { A = 0.5f * a });
                foreach (StringName action in glyph.Actions)
                {
                    Vector2 dir = DirectionOf(action);
                    bool held = Input.IsActionPressed(action);
                    Vector2 c = mid + dir * (arm * 0.5f + half * 0.5f);
                    Vector2 extent = dir.X != 0f ? new Vector2(arm, 2f * half) : new Vector2(2f * half, arm);
                    DrawRect(new Rect2(c - extent * 0.5f, extent), Ink with { A = (held ? 0.95f : 0.45f) * a });
                }

                break;
            }
            case ButtonNames.PadShape.Trigger:
                _cap.SetCornerRadiusAll(3);
                _cap.CornerRadiusTopLeft = _cap.CornerRadiusTopRight = 9;
                DrawCap(font, glyph.Text, mid, 34f, CapHeight + 2f, lit, a);
                _cap.SetCornerRadiusAll(5);
                break;
            case ButtonNames.PadShape.Shoulder:
                _cap.SetCornerRadiusAll(8);
                DrawCap(font, glyph.Text, mid, 34f, CapHeight - 3f, lit, a);
                _cap.SetCornerRadiusAll(5);
                break;
            default:
                DrawCap(font, glyph.Text, mid, glyph.Width, CapHeight, lit, a);
                break;
        }
    }

    private void DrawCap(Font font, string text, Vector2 mid, float width, float height, float lit, float a)
    {
        Rect2 rect = new(mid - new Vector2(width, height) * 0.5f, new Vector2(width, height));
        _cap.BgColor = Dark.Lerp(Ink, lit * 0.85f) with { A = (0.5f + 0.4f * lit) * a };
        _cap.BorderColor = Ink with { A = (0.45f + 0.45f * lit) * a };
        DrawStyleBox(_cap, rect);
        Color ink = Ink.Lerp(Dark, lit) with { A = 0.95f * a };
        int arrow = ButtonNames.ArrowOf(text);
        if (arrow >= 0)
        {
            // Arrow keys: a small triangle, pointing its way (0 up, 1 left, 2 down, 3 right).
            Vector2 dir = arrow switch { 0 => Vector2.Up, 1 => Vector2.Left, 2 => Vector2.Down, _ => Vector2.Right };
            Vector2 side = new(-dir.Y, dir.X);
            DrawColoredPolygon(new[] { mid + dir * 4f, mid - dir * 3f + side * 4f, mid - dir * 3f - side * 4f }, ink);
            return;
        }

        DrawString(font, new Vector2(rect.Position.X, mid.Y + CapFont * 0.36f), text, HorizontalAlignment.Center, width, CapFont, ink);
    }

    private static Vector2 DirectionOf(StringName action) =>
        action == InputActions.MoveUp ? Vector2.Up : action == InputActions.MoveDown ? Vector2.Down : action == InputActions.MoveLeft ? Vector2.Left : Vector2.Right;

    private static float Width(Font font, string text, int size) => font.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X;
}
