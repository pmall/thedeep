using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// A relic holding one of the pilgrim's abilities (Mag-grip, later Harpoon, Drill, Ballast, Heat plating),
/// floating over a pedestal. Walking up to it takes it (<see cref="Session"/>): the core drifts into the
/// pilgrim, the ability unlocks, the game saves, and it is gone for good. The node stands on the floor.
/// </summary>
public partial class AbilityRelic : Node3D
{
    [Export] public Ability Ability { get; set; } = Ability.MagGrip;

    /// <summary>The glowing core that floats and is taken.</summary>
    [Export] public Node3D Core { get; set; } = null!;

    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>Lines naming the ability and telling how to use it (shown when taken).</summary>
    [Export] public StringName NameLine { get; set; } = "ability.mag_grip.name";
    [Export] public StringName HintLine { get; set; } = "ability.mag_grip.hint";

    private Vector3 _coreRest;
    private float _time;
    private bool _taken;

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
        Core.Position = _coreRest + new Vector3(0f, 0.08f * Mathf.Sin(_time * 1.6f), 0f);
        Core.RotateY((float)delta * 0.6f);
    }

    /// <summary>The core drifts into <paramref name="target"/> (the pilgrim's chest) with a flare, then fades.</summary>
    public async Task Absorb(Node3D target, Vector3 offset)
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
        tween.Parallel().TweenProperty(Light, "light_energy", Light.LightEnergy * 3f, 0.5f);
        tween.TweenProperty(Light, "light_energy", 0f, 0.6f);
        await ToSignal(tween, Tween.SignalName.Finished);
        Core.Visible = false;
    }

    /// <summary>Already taken (from the save): only the empty pedestal remains.</summary>
    public void Empty()
    {
        _taken = true;
        Core.Visible = false;
        Light.Visible = false;
    }
}
