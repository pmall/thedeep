using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The items and the doors they open, in play: walking up to an <see cref="ItemPickup"/> takes its item (the pilgrim holds still
/// while it flies in, then its name shows; held for good, saved at once); pulling a <see cref="Lever"/> (interact, no shrine or
/// plate prompting) sets its mechanism for good; coming near a <see cref="LockedDoor"/> holding its item opens it for good (a
/// mechanism's gate opens at once, wherever the pilgrim stands), and coming near without it, the pilgrim says what it sees there,
/// once per room visit for each door. What is
/// done for good is the session's to keep (<see cref="Session.Remember"/>); the rules are the core's (<see cref="Inventory"/>,
/// <see cref="Pilgrimage"/>).
/// </summary>
public partial class Keyring : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public Session Session { get; set; } = null!;
    [Export] public Banner Banner { get; set; } = null!;
    [Export] public HintLine Hint { get; set; } = null!;
    [Export] public SideCamera Camera { get; set; } = null!;
    [Export] public RoomTravel Travel { get; set; } = null!;

    /// <summary>How close (meters, on the play plane) the pilgrim must stand to take an item.</summary>
    [Export] public float TakeRange { get; set; } = 1.2f;

    private readonly HashSet<LockedDoor> _said = new();
    private bool _taking;
    private Lever? _prompted;

    /// <summary>Raised when a lever is pulled.</summary>
    public event System.Action<Lever>? Pulled;

    /// <summary>Raised when a door opens for its item.</summary>
    public event System.Action<LockedDoor>? Opened;

    /// <summary>Raised when the pilgrim says what it sees at a door it can't open (no item for it).</summary>
    public event System.Action<LockedDoor>? Refused;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        Travel.Travelled += OnTravelled;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree()
    {
        World.RoomLoaded -= OnRoomLoaded;
        Travel.Travelled -= OnTravelled;
    }

    /// <summary>Arrived through a locked door from its far side (which needs its item): it stands open already, for good.</summary>
    private void OnTravelled(RoomExit arrival)
    {
        if (arrival.Door is { Opened: false } door && Player.Items.Holds(door.Item.Id))
        {
            door.Settle();
            Session.Remember(door.Name);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_taking || !Player.InControl)
        {
            PromptLever(null);
            return;
        }

        Room room = World.Room;
        Vector3 p = Player.GlobalPosition;
        IReadOnlyList<ItemPickup> pickups = room.All<ItemPickup>();
        for (int i = 0; i < pickups.Count; i++)
        {
            ItemPickup pickup = pickups[i];
            if (!pickup.Taken && new Vector2(pickup.GlobalPosition.X - p.X, pickup.GlobalPosition.Y - p.Y).Length() <= TakeRange)
            {
                Take(pickup);
                return;
            }
        }

        Lever? lever = Player.IsUpright && Player.IsOnFloor() && !Session.Prompting ? NearLever(room, p) : null;
        PromptLever(lever);
        if (lever is not null && Input.IsActionJustPressed(InputActions.Interact))
        {
            lever.Pull();
            Player.Items.Take(lever.Item.Id);
            Session.Remember(lever.Name);
            Camera.AddShake(0.1f);
            Pulled?.Invoke(lever);
        }

        IReadOnlyList<LockedDoor> doors = room.All<LockedDoor>();
        for (int i = 0; i < doors.Count; i++)
        {
            LockedDoor door = doors[i];
            bool mechanism = door.Item.Kind == ItemKind.Mechanism;
            if (door.Opened || (!mechanism && !door.Near(p)))
            {
                continue;
            }

            if (Player.Items.Holds(door.Item.Id))
            {
                door.Open();
                Session.Remember(door.Name);
                Camera.AddShake(0.15f);
                Opened?.Invoke(door);
            }
            else if (door.Near(p) && _said.Add(door))
            {
                Hint.Show(door.Item.LockedLine, ItemKinds.ColorOf(door.Item.Kind));
                Refused?.Invoke(door);
            }
        }
    }

    private static Lever? NearLever(Room room, Vector3 p)
    {
        IReadOnlyList<Lever> levers = room.All<Lever>();
        for (int i = 0; i < levers.Count; i++)
        {
            if (!levers[i].Pulled && levers[i].Near(p))
            {
                return levers[i];
            }
        }

        return null;
    }

    private void PromptLever(Lever? lever)
    {
        if (_prompted == lever)
        {
            return;
        }

        if (_prompted is { } shown && IsInstanceValid(shown))
        {
            shown.ShowPrompt(false);
        }

        _prompted = lever;
        lever?.ShowPrompt(true);
    }

    /// <summary>Taking an item: the pilgrim stands still while it flies in, then it is held, saved, and its name shows.</summary>
    private async void Take(ItemPickup pickup)
    {
        _taking = true;
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        await pickup.Fly(Player, new Vector3(0f, 1.05f, 0f));
        Player.Items.Take(pickup.Item.Id);
        Session.Remember(pickup.Name);
        Player.Frozen = false;
        _taking = false;
        await Banner.Show(pickup.Item.NameLine, pickup.Item.TakenLine);
    }

    private void OnRoomLoaded(Room room)
    {
        _said.Clear();
        _prompted = null;
        string name = World.NameOf(room.SceneFilePath);
        foreach (ItemPickup pickup in room.All<ItemPickup>())
        {
            if (Session.ItemTaken(name, pickup.Name, pickup.Item.Id, Player.Items.Held))
            {
                pickup.Empty();
            }
        }

        foreach (Lever lever in room.All<Lever>())
        {
            if (Session.ItemTaken(name, lever.Name, lever.Item.Id, Player.Items.Held))
            {
                lever.Settle();
            }
        }

        foreach (LockedDoor door in room.All<LockedDoor>())
        {
            if (Session.Done(name, door.Name) || (door.Item.Kind == ItemKind.Mechanism && Player.Items.Holds(door.Item.Id)))
            {
                door.Settle();
            }
        }
    }
}
