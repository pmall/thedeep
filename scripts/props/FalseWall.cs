using Godot;

namespace TheDeep;

/// <summary>
/// A hidden passage's mask (a room kind's piece): a stretch of wall that looks like the room's own rock but holds nothing back. The
/// pilgrim walking into it finds it: it fades away for good (<see cref="Secrets"/> remembers it). It is no <see cref="Block"/>, so
/// the map's planner walks through it as through air; the exit it hides is marked <see cref="RoomExit.Hidden"/> (the room check holds
/// a hidden exit to lie behind one), and the map shows that way only once taken. The node is the wall's center on the play plane.
/// </summary>
public partial class FalseWall : Node3D, IWalkedThrough
{
    /// <summary>Width, height and depth, meters, like a block's.</summary>
    [Export] public Vector3 Size { get; set; } = new(2f, 4f, 4f);

    /// <summary>The look it borrows (the room's rock, as its blocks wear).</summary>
    [Export] public Material Material { get; set; } = null!;

    /// <summary>Seconds it takes to fade away once found.</summary>
    [Export] public float FadeTime { get; set; } = 0.8f;

    private MeshInstance3D? _face;

    /// <summary>Found by the pilgrim, now or on an earlier visit: fading or gone.</summary>
    public bool Found { get; private set; }

    /// <summary>The wall on the play plane, room-local.</summary>
    public Rect2 Region => new(new Vector2(GlobalPosition.X - Size.X * 0.5f, GlobalPosition.Y - Size.Y * 0.5f), new Vector2(Size.X, Size.Y));

    public override void _Ready()
    {
        _face = new MeshInstance3D { Name = "Face", Mesh = new BlockMesh { Size = Size, Bevel = 0.05f }, MaterialOverride = Material };
        AddChild(_face);
    }

    /// <summary>Found: it fades away.</summary>
    public void Reveal()
    {
        Found = true;
        if (_face is { } face)
        {
            Tween tween = CreateTween();
            tween.TweenProperty(face, "transparency", 1f, FadeTime).SetTrans(Tween.TransitionType.Sine);
            tween.TweenCallback(Callable.From(() => face.Visible = false));
        }
    }

    /// <summary>Found before (from the save): gone already.</summary>
    public void Settle()
    {
        Found = true;
        if (_face is { } face)
        {
            face.Visible = false;
        }
    }
}
