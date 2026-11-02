using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// A power-up (docs/roadmap.md#game-systems): a shard turning over a low pedestal that adds <see cref="Amount"/> to one of the
/// pilgrim's stats for good (<see cref="Stat"/>: strength, more damage per blow; defense, more health). Walking up to it takes it
/// (<see cref="PowerUps"/>): it sinks into the pilgrim, the stat rises, the game saves, and it is gone. It opens no way, so the map
/// does not see it. The node stands on the floor.
/// </summary>
public partial class PowerUp : Node3D
{
    [Export] public PowerStat Stat { get; set; } = PowerStat.Defense;

    /// <summary>How much it adds: damage per blow (strength) or hits the pilgrim can take (defense).</summary>
    [Export] public int Amount { get; set; } = 1;

    /// <summary>The shard that turns and is taken.</summary>
    [Export] public MeshInstance3D Shard { get; set; } = null!;

    [Export] public OmniLight3D Light { get; set; } = null!;

    private Vector3 _rest;
    private float _time;

    public bool Taken { get; private set; }

    public override void _Ready()
    {
        _rest = Shard.Position;
        Color color = PowerStats.ColorOf(Stat);
        Shard.SetInstanceShaderParameter("tint", color);
        Light.LightColor = color;
    }

    public override void _Process(double delta)
    {
        if (Taken)
        {
            return;
        }

        _time += (float)delta;
        Shard.Position = _rest + new Vector3(0f, 0.07f * Mathf.Sin(_time * 1.4f), 0f);
        Shard.RotateY((float)delta * 1.1f);
    }

    /// <summary>The shard sinks into <paramref name="target"/> (the pilgrim's chest), flaring, and fades.</summary>
    public async Task Sink(Node3D target, Vector3 offset)
    {
        Taken = true;
        Vector3 start = Shard.GlobalPosition;
        Tween tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            float e = t * t * (3f - 2f * t);
            Shard.GlobalPosition = start.Lerp(target.GlobalPosition + offset, e);
            Shard.Scale = Vector3.One * (1f + 0.5f * Mathf.Sin(Mathf.Pi * t) - 0.9f * e * e);
        }), 0f, 1f, 0.9f);
        tween.Parallel().TweenProperty(Light, "light_energy", Light.LightEnergy * 2.5f, 0.4f);
        tween.TweenProperty(Light, "light_energy", 0f, 0.5f);
        await ToSignal(tween, Tween.SignalName.Finished);
        Shard.Visible = false;
    }

    /// <summary>Already taken (from the save): only the pedestal remains.</summary>
    public void Empty()
    {
        Taken = true;
        Shard.Visible = false;
        Light.Visible = false;
    }
}
