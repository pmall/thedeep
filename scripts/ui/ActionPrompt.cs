using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A prompt in the world naming what the player may do here and the button that does it: the button's glyph (keyboard or pad,
/// following the device last used) then the verb (<see cref="Verb"/>, a line), facing the camera, fading in while offered and out
/// when not, rising a little as it comes. The prop that places it (a child node, where the prompt floats) offers it each time that
/// changes (<see cref="Offer"/>); who offers keeps two prompts from showing at once (<see cref="Session.Prompting"/>).
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

    /// <summary>Its size in the world, meters per pixel of its face (the verb's letters stand about 0.35 m).</summary>
    private const float PixelSize = 0.0075f;

    /// <summary>A hold prompt held full: the act is confirmed.</summary>
    public event System.Action? Completed;

    /// <inheritdoc/>
    public IEnumerable<StringName> LineIds => new[] { Verb };

    /// <summary>Offered now: it shows (and a hold may fill).</summary>
    public bool Offered { get; private set; }

    /// <summary>How full the hold is, 0–1 (zero for a press prompt).</summary>
    public float Fill => _hold?.Fraction ?? 0f;

    private SubViewport? _viewport;
    private PromptFace? _face;
    private Sprite3D? _sprite;
    private HoldProgress? _hold;
    private ButtonNames.Device _device;
    private ButtonNames.Device _drawn;
    private StringName _drawnVerb = "";
    private float _shown;

    private SubViewport Viewport => _viewport ??= new SubViewport
    {
        TransparentBg = true,
        Disable3D = true,
        GuiDisableInput = true,
        RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
    };

    private PromptFace Face => _face ??= new PromptFace();

    private Sprite3D Sprite => _sprite ??= new Sprite3D
    {
        PixelSize = PixelSize,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
        Shaded = false,
        // Over its own prop and whatever stands before it: a prompt is never hidden.
        NoDepthTest = true,
        RenderPriority = 10,
        Visible = false,
    };

    /// <summary>Offers the act, or takes the offer back; it fades toward that.</summary>
    public void Offer(bool offered) => Offered = offered;

    public override void _Ready()
    {
        _hold = HoldSeconds > 0f ? new HoldProgress(HoldSeconds) : null;
        _device = _drawn = ButtonGlyph.FirstDevice();
        Viewport.AddChild(Face);
        AddChild(Viewport);
        AddChild(Sprite);
        Draw();
        Sprite.Texture = Viewport.GetTexture();
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
        _shown = Motion.Approach(_shown, Offered ? 1f : 0f, dt * 4f);
        bool visible = _shown > 0f;
        Sprite.Visible = visible;
        Viewport.RenderTargetUpdateMode = visible ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
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
        Sprite.Modulate = Sprite.Modulate with { A = _shown * _shown * (3f - 2f * _shown) };
        Sprite.Position = Sprite.Position with { Y = Rise * _shown };
        Sprite.Offset = new Vector2(Reach * Viewport.Size.X * 0.5f, 0f);
    }

    /// <summary>Draws the face for the device and the verb shown, sized to fit.</summary>
    private void Draw()
    {
        _drawnVerb = Verb;
        Viewport.Size = Face.Show(ButtonGlyph.Main(Action, _drawn), LineBook.Shared.Find(Verb)?.Text ?? "", _hold is not null);
    }
}
