using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A prompt naming what the player may do at a place in the world and the button that does it: the button's glyph (keyboard or pad,
/// following the device last used) then the verb (<see cref="Verb"/>, a line), fading in while offered and out when not, rising a
/// little as it comes. The prop that places it (a child node, where the prompt floats) offers it each time that changes
/// (<see cref="Offer"/>); who offers keeps two prompts from showing at once (<see cref="Session.Prompting"/>).
/// <para>It is drawn on the screen over the place (<see cref="PromptPlace"/>), never in the world, so like every other text brick it
/// keeps the style's size, ink and plate in every room: the room's grade, fog and bloom never reach it, and nothing stands before it.</para>
/// <para>A press prompt (<see cref="HoldSeconds"/> zero) only shows: the prop takes the press itself. A hold prompt rings its
/// button; while offered and held, the ring fills over <see cref="HoldSeconds"/> and the prompt raises <see cref="Completed"/>;
/// letting go empties it at once, and taps never add up (<see cref="HoldProgress"/>). The hold counts physics ticks, like the rules it
/// confirms. The verb may change while it shows (a prop that names its act per place, <see cref="LawBreak"/>).</para>
/// </summary>
public partial class ActionPrompt : Node3D, INamesLines
{
    /// <summary>The verb shown (a line id: "Pull", "Rest").</summary>
    [Export] public StringName Verb { get; set; } = "";

    /// <summary>The input action asked for; its main button is shown.</summary>
    [Export] public StringName Action { get; set; } = InputActions.Interact;

    /// <summary>How long the button must be held to act, seconds; zero for a press.</summary>
    [Export] public float HoldSeconds { get; set; }

    /// <summary>Which way it reaches from its node: 0 centered on it, +1 reaching east (its west edge at the node), −1 reaching west, so a
    /// prompt beside something bright (a veil) stays clear of it.</summary>
    [Export] public int Reach { get; set; }

    /// <summary>How far it rises as it fades in, meters.</summary>
    [Export] public float Rise { get; set; } = 0.08f;

    /// <summary>The canvas layer it is drawn on: over the health, under the hint line, the banner and the dialogue.</summary>
    private const int ScreenLayer = 72;

    /// <summary>How far inside the canvas's edges it is kept, pixels.</summary>
    private const float Margin = 16f;

    /// <summary>A hold prompt held full: the act is confirmed.</summary>
    public event System.Action? Completed;

    /// <inheritdoc/>
    public IEnumerable<StringName> LineIds => new[] { Verb };

    /// <summary>Offered now: it shows (and a hold may fill).</summary>
    public bool Offered { get; private set; }

    /// <summary>How full the hold is, 0–1 (zero for a press prompt).</summary>
    public float Fill => _hold?.Fraction ?? 0f;

    private CanvasLayer? _layer;
    private PromptFace? _face;
    private HoldProgress? _hold;
    private ButtonNames.Device _device;
    private ButtonNames.Device _drawn;
    private StringName _drawnVerb = "";
    private float _shown;

    private CanvasLayer Screen => _layer ??= new CanvasLayer { Layer = ScreenLayer, Visible = false };

    private PromptFace Face => _face ??= new PromptFace { MouseFilter = Control.MouseFilterEnum.Ignore };

    /// <summary>Offers the act, or takes the offer back; it fades toward that.</summary>
    public void Offer(bool offered) => Offered = offered;

    public override void _Ready()
    {
        _hold = HoldSeconds > 0f ? new HoldProgress(HoldSeconds) : null;
        _device = _drawn = ButtonGlyph.FirstDevice();
        Screen.AddChild(Face);
        AddChild(Screen);
        Draw();
    }

    public override void _Input(InputEvent @event) => _device = ButtonGlyph.DeviceAfter(_device, @event);

    public override void _PhysicsProcess(double delta)
    {
        if (_hold is { } hold && hold.Step(Input.IsActionPressed(Action), Offered, (float)delta))
        {
            Completed?.Invoke();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _shown = Motion.Approach(_shown, Offered ? 1f : 0f, dt / TextStyle.Shared.Panel);
        Camera3D? camera = GetViewport().GetCamera3D();
        Vector3 place = GlobalPosition + Vector3.Up * Rise * _shown;
        bool visible = _shown > 0f && IsVisibleInTree() && camera is not null && !camera.IsPositionBehind(place);
        Screen.Visible = visible;
        if (!visible)
        {
            return;
        }

        if (_device != _drawn || _drawnVerb != Verb)
        {
            _drawn = _device;
            Draw();
        }

        Face.Light(dt);
        Face.Fill = Fill;
        Face.QueueRedraw();
        Face.Modulate = Colors.White with { A = _shown * _shown * (3f - 2f * _shown) };
        Vector2 at = camera!.UnprojectPosition(place);
        Vector2 canvas = GetViewport().GetVisibleRect().Size;
        System.Numerics.Vector2 corner = PromptPlace.TopLeft(new(at.X, at.Y), new(Face.Size.X, Face.Size.Y), Reach, new(canvas.X, canvas.Y), Margin);
        // On whole pixels, so its letters stay crisp as the camera glides.
        Face.Position = new Vector2(Mathf.Round(corner.X), Mathf.Round(corner.Y));
    }

    /// <summary>Draws the face for the device and the verb shown, sized to fit.</summary>
    private void Draw()
    {
        _drawnVerb = Verb;
        Face.Show(ButtonGlyph.Main(Action, _drawn), LineBook.Shared.Find(Verb)?.Text ?? "", _hold is not null);
    }
}
