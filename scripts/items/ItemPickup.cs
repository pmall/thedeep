using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// An item lying in a room (<see cref="ItemDefinition"/>): a key, or a code burning on a tablet, floating in the cradle of a low
/// plinth, the cradle's rim and the item glowing in its kind's color (<see cref="ItemKinds"/>). Walking up to it takes it
/// (<see cref="Keyring"/>): it flies into the pilgrim, the item is held for good, the game saves, and it is gone; the rim goes dark.
/// The map sees it like a relic: the room check takes it from the box around its foot. The node stands on the floor;
/// <see cref="KeyLook"/> or <see cref="CodeLook"/> shows, by the item's kind.
/// </summary>
public partial class ItemPickup : Node3D, IWalkedThrough
{
    [Export] public ItemDefinition Item { get; set; } = null!;

    /// <summary>What floats and is taken: the key, or the code's tablet.</summary>
    [Export] public Node3D Token { get; set; } = null!;

    /// <summary>The key's look and the code's (one of them shows).</summary>
    [Export] public Node3D KeyLook { get; set; } = null!;
    [Export] public Node3D CodeLook { get; set; } = null!;

    /// <summary>The pieces that glow in the kind's color: the looks' and the cradle's rim.</summary>
    [Export] public GeometryInstance3D[] Glows { get; set; } = System.Array.Empty<GeometryInstance3D>();

    /// <summary>The cradle's rim, dark once the item is taken.</summary>
    [Export] public GeometryInstance3D Rim { get; set; } = null!;

    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>How the token floats: bobs <see cref="BobMeters"/> up and down at <see cref="BobSpeed"/> (radians per second), rocks at <see cref="RockSpeed"/>, tilts <see cref="TiltRadians"/> at <see cref="TiltSpeed"/>.</summary>
    [ExportGroup("Float")]
    [Export] public float BobMeters { get; set; } = 0.06f;
    [Export] public float BobSpeed { get; set; } = 1.9f;
    [Export] public float RockSpeed { get; set; } = 0.9f;
    [Export] public float TiltRadians { get; set; } = 0.06f;
    [Export] public float TiltSpeed { get; set; } = 1.3f;

    /// <summary>How the token is taken: flies into the pilgrim for <see cref="FlySeconds"/>, arcing <see cref="ArcMeters"/> up on the way, the rim flaring to <see cref="FlarePeak"/>.</summary>
    [ExportGroup("Take")]
    [Export] public float FlySeconds { get; set; } = 0.6f;
    [Export] public float ArcMeters { get; set; } = 0.4f;
    [Export] public float FlarePeak { get; set; } = 2.5f;

    /// <summary>How far the token rocks either side of facing the camera, radians: a flat key never turns edge-on.</summary>
    private const float Rock = 0.55f;

    private Vector3 _rest;
    private float _time;

    /// <summary>Picked up already.</summary>
    public bool Taken { get; private set; }

    public override void _Ready()
    {
        _rest = Token.Position;
        KeyLook.Visible = Item.Kind == ItemKind.Key;
        CodeLook.Visible = Item.Kind == ItemKind.Code;
        Color color = ItemKinds.ColorOf(Item.Kind);
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
        Token.Position = _rest + new Vector3(0f, BobMeters * Mathf.Sin(_time * BobSpeed), 0f);
        Token.Rotation = new Vector3(0f, Rock * Mathf.Sin(_time * RockSpeed), TiltRadians * Mathf.Sin(_time * TiltSpeed));
    }

    /// <summary>The token flies into <paramref name="target"/> (the pilgrim's chest) and fades; the cradle's rim flares and dies.</summary>
    public async Task Fly(Node3D target, Vector3 offset)
    {
        Taken = true;
        Vector3 start = Token.GlobalPosition;
        Tween tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            float e = t * t * (3f - 2f * t);
            Token.GlobalPosition = start.Lerp(target.GlobalPosition + offset, e) + new Vector3(0f, ArcMeters * Mathf.Sin(Mathf.Pi * t), 0f);
            Token.Scale = Vector3.One * (1f - 0.8f * e * e);
        }), 0f, 1f, FlySeconds);
        tween.Parallel().TweenProperty(Light, Names.LightEnergy, 0f, FlySeconds);
        TokenGlow.FlareOut(tween, Rim, FlarePeak, FlySeconds);
        await ToSignal(tween, Tween.SignalName.Finished);
        Token.Visible = false;
    }

    /// <summary>Already taken (from the save, or held): only the plinth remains, its rim dark.</summary>
    public void Empty()
    {
        Taken = true;
        Token.Visible = false;
        Light.Visible = false;
        TokenGlow.Light(Rim, TokenGlow.Spent);
    }
}
