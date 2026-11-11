using Godot;

namespace TheDeep;

/// <summary>
/// A lever (a switch, a room kind's piece): a handle on an iron housing under a quadrant, its grip and the slit on its housing
/// burning in the mechanism's color. Standing at it shows "Pull"; pulling it (interact) throws the handle across the quadrant and
/// sets its mechanism (<see cref="Item"/>, an <see cref="ItemDefinition"/> of kind <see cref="ItemKind.Mechanism"/>) for good: the
/// grip and the slit flare and die, and the gate that mechanism holds (a <see cref="LockedDoor"/> for the same item) opens at once
/// (<see cref="Keyring"/>). The map sees it as an item lying here: the solver proves the gate's way like a key's. The node stands on
/// the floor where the pilgrim pulls.
/// </summary>
public partial class Lever : Node3D
{
    [Export] public ItemDefinition Item { get; set; } = null!;

    /// <summary>The handle, turned about its pivot: up (set) to down (pulled).</summary>
    [Export] public Node3D Handle { get; set; } = null!;

    /// <summary>The pieces that glow in the mechanism's color while it waits (the grip, the housing's slit), dark once pulled.</summary>
    [Export] public GeometryInstance3D[] Glows { get; set; } = System.Array.Empty<GeometryInstance3D>();

    [Export] public Label3D Prompt { get; set; } = null!;

    /// <summary>How near the pilgrim must stand, meters on the play plane.</summary>
    [Export] public float Range { get; set; } = 1.4f;

    /// <summary>The handle's angle set and pulled, degrees about the depth axis.</summary>
    private const float SetAngle = 40f, PulledAngle = -40f;

    private float _shown;
    private bool _wanted;

    /// <summary>Thrown already.</summary>
    public bool Pulled { get; private set; }

    public override void _Ready()
    {
        Prompt.Text = LineBook.Shared.Find(GameLines.Pull)?.Text ?? "";
        Prompt.Modulate = Prompt.Modulate with { A = 0f };
        Handle.RotationDegrees = new Vector3(0f, 0f, SetAngle);
        Color color = ItemKinds.ColorOf(ItemKind.Mechanism);
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.Tint(glow, color);
            TokenGlow.Light(glow, 1f);
        }
    }

    /// <summary>Whether feet at <paramref name="feet"/> are near enough to pull it.</summary>
    public bool Near(Vector3 feet) => new Vector2(feet.X - GlobalPosition.X, feet.Y - GlobalPosition.Y).Length() <= Range;

    /// <summary>Whether the pull prompt is wanted now; it fades in and out toward that.</summary>
    public void ShowPrompt(bool shown) => _wanted = shown;

    /// <summary>Thrown: the handle swings down across the quadrant and bounces on its stop, the glow flares and dies.</summary>
    public void Pull()
    {
        Pulled = true;
        _wanted = false;
        Tween tween = CreateTween();
        tween.TweenProperty(Handle, "rotation_degrees:z", PulledAngle, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.FlareOut(tween, glow, 2.2f, 1f);
        }
    }

    /// <summary>Pulled before (from the save, or its mechanism already set): down already.</summary>
    public void Settle()
    {
        Pulled = true;
        Handle.RotationDegrees = new Vector3(0f, 0f, PulledAngle);
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.Light(glow, TokenGlow.Spent);
        }
    }

    public override void _Process(double delta)
    {
        _shown = Motion.Approach(_shown, _wanted ? 1f : 0f, (float)delta * 4f);
        Prompt.Modulate = Prompt.Modulate with { A = _shown * _shown * (3f - 2f * _shown) * 0.85f };
    }
}
