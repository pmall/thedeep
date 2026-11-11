using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// A power-up (docs/vision.md#game-systems): a crystal floating in the cradle of a low pedestal that adds <see cref="Amount"/> to one
/// of the pilgrim's stats for good (<see cref="Stat"/>: strength, more damage per blow; defense, more health). Each stat has its own
/// look as well as its color (<see cref="PowerStats"/>), so they never read alike: strength a crown of blades
/// (<see cref="StrengthLook"/>), defense a cut stone held in two warding rings (<see cref="DefenseLook"/>). Walking up to it takes it
/// (<see cref="PowerUps"/>): it sinks into the pilgrim, the stat rises, the game saves, and it is gone; the cradle's rim goes dark.
/// It opens no way, so the map does not see it. The node stands on the floor.
/// </summary>
public partial class PowerUp : Node3D
{
    [Export] public PowerStat Stat { get; set; } = PowerStat.Defense;

    /// <summary>How much it adds: damage per blow (strength) or hits the pilgrim can take (defense).</summary>
    [Export] public int Amount { get; set; } = 1;

    /// <summary>What turns and is taken: the stat's look.</summary>
    [Export] public Node3D Token { get; set; } = null!;

    /// <summary>The strength's look and the defense's (one of them shows).</summary>
    [Export] public Node3D StrengthLook { get; set; } = null!;
    [Export] public Node3D DefenseLook { get; set; } = null!;

    /// <summary>The pieces that glow in the stat's color: the looks' and the cradle's rim.</summary>
    [Export] public GeometryInstance3D[] Glows { get; set; } = System.Array.Empty<GeometryInstance3D>();

    /// <summary>The cradle's rim, dark once the power-up is taken.</summary>
    [Export] public GeometryInstance3D Rim { get; set; } = null!;

    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>How far the token rocks either side of facing the camera, radians: the look keeps its face (its crystals spin on their own, <see cref="Spinner"/>).</summary>
    private const float Rock = 0.45f;

    private Vector3 _rest;
    private float _time;

    /// <summary>Taken already.</summary>
    public bool Taken { get; private set; }

    public override void _Ready()
    {
        _rest = Token.Position;
        StrengthLook.Visible = Stat == PowerStat.Strength;
        DefenseLook.Visible = Stat == PowerStat.Defense;
        Color color = PowerStats.ColorOf(Stat);
        Light.LightColor = color;
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.Tint(glow, color);
            TokenGlow.Light(glow, 1f);
        }
    }

    public override void _Process(double delta)
    {
        if (Taken)
        {
            return;
        }

        _time += (float)delta;
        Token.Position = _rest + new Vector3(0f, 0.07f * Mathf.Sin(_time * 1.4f), 0f);
        Token.Rotation = new Vector3(0f, Rock * Mathf.Sin(_time * 0.8f), 0f);
    }

    /// <summary>The token sinks into <paramref name="target"/> (the pilgrim's chest), flaring, and fades; the cradle's rim dies.</summary>
    public async Task Sink(Node3D target, Vector3 offset)
    {
        Taken = true;
        Vector3 start = Token.GlobalPosition;
        Tween tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            float e = t * t * (3f - 2f * t);
            Token.GlobalPosition = start.Lerp(target.GlobalPosition + offset, e);
            Token.Scale = Vector3.One * (1f + 0.5f * Mathf.Sin(Mathf.Pi * t) - 0.9f * e * e);
        }), 0f, 1f, 0.9f);
        TokenGlow.FlareOut(tween, Rim, 2.5f, 0.9f);
        tween.Parallel().TweenProperty(Light, "light_energy", Light.LightEnergy * 2.5f, 0.4f);
        tween.TweenProperty(Light, "light_energy", 0f, 0.5f);
        await ToSignal(tween, Tween.SignalName.Finished);
        Token.Visible = false;
    }

    /// <summary>Already taken (from the save): only the pedestal remains, its rim dark.</summary>
    public void Empty()
    {
        Taken = true;
        Token.Visible = false;
        Light.Visible = false;
        TokenGlow.Light(Rim, TokenGlow.Spent);
    }
}
