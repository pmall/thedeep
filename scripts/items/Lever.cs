using Godot;

namespace TheDeep;

/// <summary>
/// A lever (a switch, a room kind's piece): a handle on an iron housing under a quadrant, its grip and the slit on its housing
/// burning in the mechanism's color. Standing at it offers its prompt (an <see cref="ActionPrompt"/>, "Pull"); pulling it (interact) throws the handle across the quadrant and
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

    [Export] public ActionPrompt Prompt { get; set; } = null!;

    /// <summary>How near the pilgrim must stand, meters on the play plane.</summary>
    [Export] public float Range { get; set; } = 1.4f;

    /// <summary>How long the handle takes to swing down, seconds.</summary>
    [Export] public float PullSeconds { get; set; } = 0.35f;

    /// <summary>How bright the glow flares as it is pulled (<see cref="TokenGlow.FlareOut"/>'s peak) and how long it takes to die, seconds.</summary>
    [Export] public float FlarePeak { get; set; } = 2.2f;
    [Export] public float FlareSeconds { get; set; } = 1f;

    /// <summary>The handle's angle set and pulled, degrees about the depth axis.</summary>
    private const float SetAngle = 40f, PulledAngle = -40f;

    /// <summary>Thrown already.</summary>
    public bool Pulled { get; private set; }

    /// <summary>Raised when the pilgrim pulls the lever (never during <see cref="Settle"/>).</summary>
    public event System.Action<Lever>? Thrown;

    public override void _Ready()
    {
        Handle.RotationDegrees = new Vector3(0f, 0f, SetAngle);
        Color color = ItemKinds.ColorOf(ItemKind.Mechanism);
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.Tint(glow, color);
            TokenGlow.Light(glow, 1f);
        }
    }

    /// <summary>Whether feet at <paramref name="feet"/> are near enough to pull it.</summary>
    public bool Near(Vector3 feet) => feet.WithinOnPlane(GlobalPosition, Range);

    /// <summary>Thrown: the handle swings down across the quadrant and bounces on its stop, the glow flares and dies.</summary>
    public void Pull()
    {
        if (Pulled)
        {
            return;
        }

        Pulled = true;
        Prompt.Offer(false);
        Tween tween = CreateTween();
        tween.TweenProperty(Handle, Names.RollDegrees, PulledAngle, PullSeconds).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.FlareOut(tween, glow, FlarePeak, FlareSeconds);
        }

        Thrown?.Invoke(this);
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
}
