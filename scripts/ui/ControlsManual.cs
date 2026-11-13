using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Debug builds: a small manual on screen, each button lit while held. Two of them (DebugHud): the gameplay manual, bottom left
/// (<see cref="Gameplay"/>: each action's main button and what it does; keyboard caps while the player is on the keyboard, gamepad
/// buttons while on the pad, <see cref="ButtonNames.Next"/>, crossfading when the device changes; F8 hides it), and the debug keys,
/// top left with the F7 readout (<see cref="Debug"/>: the keyboard key, then the pad button where there is one). Buttons are read
/// from the input map, keys by the layout's own labels (on AZERTY the physical WASD reads Z Q S D), so the manual never drifts
/// from the bindings. An ability's rows are faint until it is held. Words come from the line book, written on the plate in the shared
/// <see cref="TextStyle"/>.
/// </summary>
public partial class ControlsManual : Control
{
    /// <summary>The pilgrim whose abilities dim the rows it can't use yet.</summary>
    public required Player Player { get; init; }

    /// <summary>The device shown; set by the last input, or forced (UiShot).</summary>
    public ButtonNames.Device Device { get; set; }

    private const float CapGap = 4f;
    private const float Column = 10f;

    private static TextStyle Style => TextStyle.Shared;

    /// <summary>A row's height: a key cap's, or the words' size, whichever is taller, and a little air.</summary>
    private float RowHeight => Mathf.Max(_painter.CapHeight, Style.Caption) + 3f;

    /// <summary>A header's height: a row's, and a little more air above.</summary>
    private float HeaderHeight => RowHeight + 1f;

    /// <summary>The size of the marks between the caps, as their own letters.</summary>
    private int CapFont => _painter.CapFont;

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

    /// <summary>How far below the screen's top the manual sits when <see cref="Top"/>, in pixels.</summary>
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
        /// <summary>The mark's text.</summary>
        public string Text { get; } = text;
    }

    /// <summary>One button on screen, drawn by the <see cref="ButtonPainter"/>.</summary>
    private sealed class Glyph(ButtonGlyph button) : Item
    {
        /// <summary>The button and how lit it is.</summary>
        public ButtonGlyph Button { get; } = button;
    }

    /// <summary>A row as drawn: its spec, its words, and for each device its buttons and the separators between them.</summary>
    private sealed class Row(Spec spec, string words, bool header)
    {
        /// <summary>What the row is made from.</summary>
        public Spec Spec { get; } = spec;
        /// <summary>What the row says its buttons do.</summary>
        public string Words { get; } = words;
        /// <summary>A title row, not a control.</summary>
        public bool Header { get; } = header;
        /// <summary>The row's buttons and separators, one list per device.</summary>
        public readonly List<Item>[] Items = { new(), new() };
    }

    private readonly List<Row> _rows = new();
    private ButtonNames.Device _shown;
    private float _alpha = 1f;
    private Vector2 _stick;
    private readonly ButtonPainter _painter = new();
    private StyleBoxFlat? _panel;
    private StyleBoxFlat PanelBox => _panel ??= Style.NewPlate();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Font font = Style.Font;
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

        Device = _shown = BothDevices ? ButtonNames.Device.Keyboard : ButtonGlyph.FirstDevice();
    }

    public override void _Input(InputEvent @event)
    {
        if (BothDevices)
        {
            return;
        }

        Device = ButtonGlyph.DeviceAfter(Device, @event);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
        {
            return;
        }

        float dt = (float)delta;
        _alpha = Motion.Approach(_alpha, Device == _shown ? 1f : 0f, dt / (0.5f * Style.Panel)); // out, then in, within a panel's ease
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
                    glyph.Button.Light(dt);
                }
            }
        }

        Vector2 stick = Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown);
        _stick = _stick.Lerp(stick, 1f - Mathf.Exp(-dt * 30f));
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            _painter.Dispose();
            _panel?.Dispose();
        }
    }

    public override void _Draw()
    {
        TextStyle style = Style;
        Font font = style.Font;
        int words = style.Caption;
        float rowHeight = RowHeight, headerHeight = HeaderHeight;
        // Text sits on a row's middle line: half the font's ascent over its descent below the baseline.
        float centered = 0.5f * (font.GetAscent(words) - font.GetDescent(words));
        float marks = 0.5f * (font.GetAscent(CapFont) - font.GetDescent(CapFont));
        int device = (int)_shown;
        float a = _alpha * _alpha * (3f - 2f * _alpha);

        float buttons = 0f;
        float wordsWidth = 0f;
        float height = 0f;
        foreach (Row row in _rows)
        {
            buttons = Mathf.Max(buttons, ItemsWidth(row.Items[device]));
            wordsWidth = Mathf.Max(wordsWidth, Width(font, row.Header ? row.Words.ToUpperInvariant() : row.Words, words));
            height += row.Header ? headerHeight : rowHeight;
        }

        Vector2 pad = style.Padding;
        Vector2 size = new(pad.X * 2f + buttons + Column + wordsWidth, pad.Y * 2f + height);
        Vector2 origin = new(16f, Top ? TopOffset : Size.Y - 16f - size.Y);
        DrawStyleBox(PanelBox, new Rect2(origin, size));

        float y = origin.Y + pad.Y;
        float wordsX = origin.X + pad.X + buttons + Column;
        foreach (Row row in _rows)
        {
            if (row.Header)
            {
                float line = y + headerHeight * 0.5f + 1f;
                DrawString(font, new Vector2(origin.X + pad.X, line + centered), row.Words.ToUpperInvariant(), HorizontalAlignment.Left, -1f, words,
                    style.Dim with { A = 0.7f * a });
                float after = origin.X + pad.X + Width(font, row.Words.ToUpperInvariant(), words) + Column;
                DrawLine(new Vector2(Mathf.Max(wordsX, after), line), new Vector2(origin.X + size.X - pad.X, line), style.Dim with { A = 0.2f * a }, 1f, true);
                y += headerHeight;
                continue;
            }

            float rowA = a * (row.Spec.Needs == Ability.None || Player.Abilities.Has(row.Spec.Needs) ? 1f : 0.5f);
            float mid = y + rowHeight * 0.5f;
            float x = origin.X + pad.X;
            List<Item> items = row.Items[device];
            if (items.Count == 0)
            {
                DrawString(font, new Vector2(x, mid + marks), "—", HorizontalAlignment.Left, -1f, CapFont, style.Dim with { A = 0.6f * rowA });
            }

            foreach (Item item in items)
            {
                switch (item)
                {
                    case Glyph glyph:
                        _painter.Draw(this, font, glyph.Button, new Vector2(x + glyph.Width * 0.5f, mid + 1.5f * glyph.Button.Lit), rowA, _stick);
                        break;
                    case Separator separator:
                        DrawString(font, new Vector2(x, mid + marks), separator.Text, HorizontalAlignment.Left, -1f, CapFont, style.Dim with { A = rowA });
                        break;
                }

                x += item.Width + CapGap;
            }

            DrawString(font, new Vector2(wordsX, mid + centered), row.Words, HorizontalAlignment.Left, -1f, words, style.Ink with { A = 0.9f * rowA });
            y += rowHeight;
        }
    }

    // ---- building the rows ----

    private void Build(List<Item> items, Spec spec, bool pad, Font font)
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
                    glyph.Width = _painter.Width(font, glyph.Button);
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

                if (glyph.Button.Shape is ButtonNames.PadShape.Stick or ButtonNames.PadShape.Dpad)
                {
                    if (whole is null)
                    {
                        whole = glyph;
                        whole.Button.Push = Vector2.Zero;
                    }
                    else
                    {
                        whole.Button.Actions = [.. whole.Button.Actions, actions[i]];
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

    private static Glyph? GlyphOf(InputEvent e, bool pad, StringName action) =>
        ButtonGlyph.Of(e, pad, action) is { } button ? new Glyph(button) : null;

    // ---- drawing ----

    private static float ItemsWidth(List<Item> items)
    {
        float w = 0f;
        foreach (Item item in items)
        {
            w += item.Width + CapGap;
        }

        return Mathf.Max(0f, w - CapGap);
    }

    private static float Width(Font font, string text, int size) => font.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X;
}
