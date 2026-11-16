using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// A relic holding one of the pilgrim's abilities (<see cref="Ability"/>),
/// floating over a pedestal. Walking up to it takes it (<see cref="Session"/>): the core drifts into the
/// pilgrim, the ability unlocks, the game saves, and it is gone for good. The node stands on the floor.
/// </summary>
public partial class AbilityRelic : Node3D, IAnnounces, IWalkedThrough
{
    [Export] public Ability Ability { get; set; } = Ability.MagGrip;

    /// <summary>The glowing core that floats and is taken.</summary>
    [Export] public Node3D Core { get; set; } = null!;

    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>Lines naming the ability and telling how to use it (shown when taken); a room sets both (the line check fails an unset one).</summary>
    [Export] public StringName NameLine { get; set; } = Names.None;
    [Export] public StringName HintLine { get; set; } = Names.None;

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds => new[] { NameLine, HintLine };

    /// <summary>What the banner says as it is taken.</summary>
    public Announcement Said => new(NameLine, HintLine);

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<Announcement> Announcements => new[] { Said };

    /// <summary>How the core hovers over its pedestal: its bob's height (meters), its bob's and its spin's speed (radians per second).</summary>
    [Export] public float BobHeight { get; set; } = 0.08f;
    [Export] public float BobSpeed { get; set; } = 1.6f;
    [Export] public float SpinSpeed { get; set; } = 0.6f;

    private Vector3 _coreRest;
    private float _time;
    private bool _taken;

    /// <summary>Taken already.</summary>
    public bool Taken => _taken;

    public override void _Ready()
    {
        _coreRest = Core.Position;
    }

    public override void _Process(double delta)
    {
        if (_taken)
        {
            return;
        }

        _time += (float)delta;
        Core.Position = _coreRest + new Vector3(0f, BobHeight * Mathf.Sin(_time * BobSpeed), 0f);
        Core.RotateY((float)delta * SpinSpeed);
    }

    /// <summary>The core drifts into <paramref name="target"/> (the pilgrim's chest) with a flare, then fades. False if the room was freed before it landed.</summary>
    public async Task<bool> Absorb(Node3D target, Vector3 offset)
    {
        _taken = true;
        Vector3 start = Core.GlobalPosition;
        Tween tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            float e = t * t * (3f - 2f * t);
            Core.GlobalPosition = start.Lerp(target.GlobalPosition + offset, e) + new Vector3(0f, 0.6f * Mathf.Sin(Mathf.Pi * t), 0f);
            Core.Scale = Vector3.One * (1f + 0.6f * Mathf.Sin(Mathf.Pi * t) - 0.9f * e * e);
        }), 0f, 1f, 1.1f);
        tween.Parallel().TweenProperty(Light, Names.LightEnergy, Light.LightEnergy * 3f, 0.5f);
        tween.TweenProperty(Light, Names.LightEnergy, 0f, 0.6f);
        if (!await TokenGlow.Played(this, tween))
        {
            return false;
        }

        Core.Visible = false;
        return true;
    }

    /// <summary>Already taken (from the save): only the empty pedestal remains.</summary>
    public void Empty()
    {
        _taken = true;
        Core.Visible = false;
        Light.Visible = false;
    }
}
