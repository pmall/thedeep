using Godot;

namespace TheDeep;

/// <summary>
/// A way barred until the pilgrim holds an item (<see cref="Item"/>): a portcullis with a lock (a key's) or a seal (a code's) on its
/// frame. It belongs to one exit (<see cref="RoomExit.Door"/>), whose requirement names the item, and so does the way back through
/// it, unless that one is one-way (the room check holds them to it): the solver sees the door in the requirement, the planner
/// walks up to it. Coming near holding the item opens it for good (<see cref="Keyring"/>); without it, the pilgrim says what it
/// sees. Like the portcullis, it is no <see cref="Block"/>: the geometry check treats the way as open to the body.
/// </summary>
public partial class LockedDoor : Node3D
{
    [Export] public ItemDefinition Item { get; set; } = null!;

    [Export] public Portcullis Gate { get; set; } = null!;

    /// <summary>The lock or the seal: lit in the item kind's color while shut, dark once open.</summary>
    [Export] public MeshInstance3D Lock { get; set; } = null!;

    /// <summary>How near the pilgrim must come (meters, on the play plane) for the door to know it.</summary>
    [Export] public float Range { get; set; } = 2.2f;

    public bool Opened { get; private set; }

    public override void _Ready()
    {
        Gate.Settle(open: false);
        Lock.SetInstanceShaderParameter("tint", ItemKinds.ColorOf(Item.Kind));
    }

    /// <summary>Whether a body with its feet at <paramref name="feet"/> is near enough.</summary>
    public bool Near(Vector3 feet) => new Vector2(feet.X - GlobalPosition.X, feet.Y - GlobalPosition.Y).Length() <= Range;

    /// <summary>The lock turns and the gate rises.</summary>
    public void Open()
    {
        Opened = true;
        Gate.Open();
        Lock.SetInstanceShaderParameter("tint", ItemKinds.ColorOf(Item.Kind) * 0.15f);
    }

    /// <summary>Opened before (from the save): up already.</summary>
    public void Settle()
    {
        Opened = true;
        Gate.Settle(open: true);
        Lock.SetInstanceShaderParameter("tint", ItemKinds.ColorOf(Item.Kind) * 0.15f);
    }
}
