using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// An item lying in a room (<see cref="ItemDefinition"/>): a key on a plinth, or a code burning on a tablet. Walking up to it takes it
/// (<see cref="Keyring"/>): it flies into the pilgrim, the item is held for good, the game saves, and it is gone. The map sees it like
/// a relic: the room check takes it from the box around its foot. The node stands on the floor; <see cref="KeyLook"/> or
/// <see cref="CodeLook"/> shows, by the item's kind.
/// </summary>
public partial class ItemPickup : Node3D
{
    [Export] public ItemDefinition Item { get; set; } = null!;

    /// <summary>What floats and is taken: the key, or the code's glyph.</summary>
    [Export] public Node3D Token { get; set; } = null!;

    /// <summary>The key's look and the code's (one of them shows).</summary>
    [Export] public Node3D KeyLook { get; set; } = null!;
    [Export] public Node3D CodeLook { get; set; } = null!;

    [Export] public OmniLight3D Light { get; set; } = null!;

    private Vector3 _rest;
    private float _time;

    public bool Taken { get; private set; }

    public override void _Ready()
    {
        _rest = Token.Position;
        KeyLook.Visible = Item.Kind == ItemKind.Key;
        CodeLook.Visible = Item.Kind == ItemKind.Code;
        Color color = ItemKinds.ColorOf(Item.Kind);
        Light.LightColor = color;
        foreach (Node3D look in new[] { KeyLook, CodeLook })
        {
            foreach (Node child in look.GetChildren())
            {
                if (child is GeometryInstance3D mesh)
                {
                    mesh.SetInstanceShaderParameter("tint", color);
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (Taken)
        {
            return;
        }

        _time += (float)delta;
        Token.Position = _rest + new Vector3(0f, 0.06f * Mathf.Sin(_time * 1.9f), 0f);
        if (Item.Kind == ItemKind.Key)
        {
            Token.RotateY((float)delta * 0.9f);
        }
    }

    /// <summary>The token flies into <paramref name="target"/> (the pilgrim's chest) and fades.</summary>
    public async Task Fly(Node3D target, Vector3 offset)
    {
        Taken = true;
        Vector3 start = Token.GlobalPosition;
        Tween tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            float e = t * t * (3f - 2f * t);
            Token.GlobalPosition = start.Lerp(target.GlobalPosition + offset, e) + new Vector3(0f, 0.4f * Mathf.Sin(Mathf.Pi * t), 0f);
            Token.Scale = Vector3.One * (1f - 0.8f * e * e);
        }), 0f, 1f, 0.6f);
        tween.Parallel().TweenProperty(Light, "light_energy", 0f, 0.6f);
        await ToSignal(tween, Tween.SignalName.Finished);
        Token.Visible = false;
    }

    /// <summary>Already taken (from the save, or held): only the plinth remains.</summary>
    public void Empty()
    {
        Taken = true;
        Token.Visible = false;
        Light.Visible = false;
    }
}
