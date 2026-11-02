using Godot;

namespace TheDeep;

/// <summary>
/// A lever (a switch, a room kind's piece): standing at it shows "Pull"; pulling it (interact) throws the handle and sets its
/// mechanism (<see cref="Item"/>, an <see cref="ItemDefinition"/> of kind <see cref="ItemKind.Mechanism"/>) for good: the gate that
/// mechanism holds (a <see cref="LockedDoor"/> for the same item) opens at once (<see cref="Keyring"/>). The map sees it as an item
/// lying here: the solver proves the gate's way like a key's. The node stands on the floor where the pilgrim pulls.
/// </summary>
public partial class Lever : Node3D
{
    [Export] public ItemDefinition Item { get; set; } = null!;

    /// <summary>The handle, turned about its pivot: up (set) to down (pulled).</summary>
    [Export] public Node3D Handle { get; set; } = null!;

    /// <summary>The knob's glow, in the mechanism's color while set, dark once pulled.</summary>
    [Export] public MeshInstance3D Knob { get; set; } = null!;

    [Export] public Label3D Prompt { get; set; } = null!;

    /// <summary>How near the pilgrim must stand, meters on the play plane.</summary>
    [Export] public float Range { get; set; } = 1.4f;

    /// <summary>The handle's angle set and pulled, degrees about the depth axis.</summary>
    private const float SetAngle = 40f, PulledAngle = -40f;

    private float _shown;
    private bool _wanted;

    public bool Pulled { get; private set; }

    public override void _Ready()
    {
        Prompt.Text = LineBook.Shared.Find("ui.pull")?.Text ?? "";
        Prompt.Modulate = Prompt.Modulate with { A = 0f };
        Handle.RotationDegrees = new Vector3(0f, 0f, SetAngle);
        Knob.SetInstanceShaderParameter("tint", ItemKinds.ColorOf(ItemKind.Mechanism));
    }

    public bool Near(Vector3 feet) => new Vector2(feet.X - GlobalPosition.X, feet.Y - GlobalPosition.Y).Length() <= Range;

    public void ShowPrompt(bool shown) => _wanted = shown;

    /// <summary>Thrown: the handle swings down, the knob dims.</summary>
    public void Pull()
    {
        Pulled = true;
        _wanted = false;
        CreateTween().TweenProperty(Handle, "rotation_degrees:z", PulledAngle, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        Knob.SetInstanceShaderParameter("tint", ItemKinds.ColorOf(ItemKind.Mechanism) * 0.15f);
    }

    /// <summary>Pulled before (from the save, or its mechanism already set): down already.</summary>
    public void Settle()
    {
        Pulled = true;
        Handle.RotationDegrees = new Vector3(0f, 0f, PulledAngle);
        Knob.SetInstanceShaderParameter("tint", ItemKinds.ColorOf(ItemKind.Mechanism) * 0.15f);
    }

    public override void _Process(double delta)
    {
        _shown = Motion.Approach(_shown, _wanted ? 1f : 0f, (float)delta * 4f);
        Prompt.Modulate = Prompt.Modulate with { A = _shown * _shown * (3f - 2f * _shown) * 0.85f };
    }
}
