using Godot;

namespace TheDeep;

/// <summary>
/// The lock a <see cref="LockedDoor"/> shows for one kind of item, set on its gate: a key's boss with its keyhole, a code's seal, a
/// mechanism's cog. Its <see cref="Glows"/> burn in the kind's color while the door is shut; as it opens, <see cref="Turning"/>
/// turns (the key's keyhole a quarter turn, the cog round with the winch), the glow flares and dies. The door shows the one lock
/// whose <see cref="Kind"/> is its item's.
/// </summary>
public partial class DoorLock : Node3D
{
    [Export] public ItemKind Kind { get; set; } = ItemKind.Key;

    /// <summary>What turns about the depth axis as the lock opens.</summary>
    [Export] public Node3D Turning { get; set; } = null!;

    /// <summary>How far it turns, degrees (counterclockwise as the camera sees it), and in how many seconds.</summary>
    [Export] public float TurnDegrees { get; set; } = 90f;
    [Export] public float TurnTime { get; set; } = 0.5f;

    /// <summary>The pieces that glow in the kind's color.</summary>
    [Export] public GeometryInstance3D[] Glows { get; set; } = System.Array.Empty<GeometryInstance3D>();

    /// <summary>Lit, in <paramref name="color"/>, waiting for its item.</summary>
    public void Wait(Color color)
    {
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.Tint(glow, color);
            TokenGlow.Light(glow, 1f);
        }
    }

    /// <summary>Opening: it turns, flares and goes dark.</summary>
    public void Release()
    {
        Tween tween = CreateTween();
        tween.TweenProperty(Turning, "rotation_degrees:z", Turning.RotationDegrees.Z + TurnDegrees, TurnTime)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.FlareOut(tween, glow, 2f, TurnTime + 0.6f);
        }
    }

    /// <summary>Opened before: turned and dark already.</summary>
    public void Spend()
    {
        Turning.RotationDegrees = Turning.RotationDegrees with { Z = Turning.RotationDegrees.Z + TurnDegrees };
        foreach (GeometryInstance3D glow in Glows)
        {
            TokenGlow.Light(glow, TokenGlow.Spent);
        }
    }
}
