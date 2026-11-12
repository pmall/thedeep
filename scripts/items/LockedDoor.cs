using Godot;

namespace TheDeep;

/// <summary>
/// A way barred until the pilgrim holds an item (<see cref="Item"/>): a portcullis with its kind's lock on its bars (a key's boss,
/// a code's seal, a mechanism's cog: <see cref="DoorLock"/>), burning in the kind's color. It belongs to one exit
/// (<see cref="RoomExit.Door"/>), whose requirement names the item, and so does the way back through it, unless that one is one-way
/// (the room check holds them to it): the solver sees the door in the requirement, the planner walks up to it. Coming near holding
/// the item opens it for good (<see cref="Keyring"/>): the lock turns, flares and dies, and the gate rises with it; without it, the
/// pilgrim says what it needs as soon as the door is on screen. Like the portcullis, it is no <see cref="Block"/>: the geometry check treats the way as open to the body.
/// </summary>
public partial class LockedDoor : Node3D
{
    [Export] public ItemDefinition Item { get; set; } = null!;

    [Export] public Portcullis Gate { get; set; } = null!;

    /// <summary>A lock for each kind of item; the one for <see cref="Item"/>'s kind shows, the others are hidden.</summary>
    [Export] public DoorLock[] Locks { get; set; } = System.Array.Empty<DoorLock>();

    /// <summary>The lock's light, in the kind's color while shut, out once open.</summary>
    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>How near the pilgrim must come (meters, on the play plane) for the door to know it.</summary>
    [Export] public float Range { get; set; } = 2.2f;

    private DoorLock? _lock;

    /// <summary>Where the door is looked at (its lock's height): on screen, the pilgrim sees it.</summary>
    public Vector3 Sight => GlobalPosition + new Vector3(0f, 1.2f, 0f);

    /// <summary>Unlocked: the way through is open.</summary>
    public bool Opened { get; private set; }

    public override void _Ready()
    {
        Gate.Settle(open: false);
        Color color = ItemKinds.ColorOf(Item.Kind);
        foreach (DoorLock doorLock in Locks)
        {
            doorLock.Visible = doorLock.Kind == Item.Kind;
            if (doorLock.Visible)
            {
                _lock = doorLock;
                doorLock.Wait(color);
            }
        }

        Light.LightColor = color;
    }

    /// <summary>Whether a body with its feet at <paramref name="feet"/> is near enough.</summary>
    public bool Near(Vector3 feet) => new Vector2(feet.X - GlobalPosition.X, feet.Y - GlobalPosition.Y).Length() <= Range;

    /// <summary>The lock turns and the gate rises.</summary>
    public void Open()
    {
        Opened = true;
        Gate.Open();
        _lock?.Release();
        CreateTween().TweenProperty(Light, "light_energy", 0f, 1.2f).SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>Opened before (from the save): up already.</summary>
    public void Settle()
    {
        Opened = true;
        Gate.Settle(open: true);
        _lock?.Spend();
        Light.Visible = false;
    }
}
