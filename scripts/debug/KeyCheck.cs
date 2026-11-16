using System;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// An item and its door in place, through the real input actions (run by tools/check.sh): the Sacristy key
/// (`-- --room Narthex --key-check`, <see cref="Sacristy"/>) and the Pyre's litany, a code (`-- --room Ashfall --code-check`,
/// <see cref="Pyre"/>). Without the item, the pilgrim says what the door needs as soon as it is on screen, before coming near, and a
/// second line said meanwhile is read after it; walking into the door is stopped by it; in the item's room,
/// walking up to it takes it (held, remembered, in the save); back holding it, coming near the door opens it, and walking on crosses
/// beyond; walking back out (where the room beyond lets one turn at once) the door stands open for good, and reloading its room finds
/// it open. Each step has a time limit. Prints `KEY OK` (or `CODE OK`) or `KEY CHECK` (`CODE CHECK`) errors.
/// </summary>
public partial class KeyCheck : StepDriver
{
    /// <summary>What is checked: the item, the room of its door (the check starts there), the room it lies in, the room beyond the
    /// door, which way the door is walked into (-1 west), the abilities the way needs, whether the check walks back out through it,
    /// and the word its result prints.</summary>
    public sealed record Plan(string Item, string DoorRoom, string ItemRoom, string Beyond, int Toward, Ability[] Needs, bool WalkBack, string Tag);

    /// <summary>The Sacristy key: its door in the Narthex, the key in the Scriptorium; the check walks back out.</summary>
    public static readonly Plan Sacristy = new("SacristyKey", "Narthex", "Scriptorium", "Sacristy", -1, Array.Empty<Ability>(), true, "KEY");

    /// <summary>The Pyre's ambush gate stands just inside its door, so the check does not turn back there.</summary>
    public static readonly Plan Pyre = new("PyreLitany", "Ashfall", "Belfry", "Pyre", 1, new[] { Ability.HeatPlating }, false, "CODE");

    /// <summary>The running game's items and the doors they open.</summary>
    public required Keyring Keyring { get; init; }
    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }
    /// <summary>The running game's hint line, where the door's line is read.</summary>
    public required HintLine Hint { get; init; }
    /// <summary>The plan this run checks.</summary>
    public Plan Checked { get; init; } = Sacristy;

    private string Key => Checked.Item;
    private StringName Toward => Checked.Toward < 0 ? InputActions.MoveLeft : InputActions.MoveRight;
    private StringName Back => Checked.Toward < 0 ? InputActions.MoveRight : InputActions.MoveLeft;

    private bool _refused, _opened;

    private LockedDoor? Door => World.Room.First<LockedDoor>();

    private void Load(string room) => World.Load(GD.Load<PackedScene>(World.RoomPath(room)));

    protected override void Build()
    {
        Keyring.Refused += _ => _refused = true;
        Keyring.Opened += _ => _opened = true;
        foreach (Ability ability in Checked.Needs)
        {
            Player.Abilities.Unlock(ability);
        }

        float doorX = Door?.GlobalPosition.X ?? 0f;
        Vector3 doorFloor = Door?.GlobalPosition ?? Vector3.Zero;
        Vector3 before = doorFloor - new Vector3(3f * Checked.Toward, 0f, 0f);
        bool Shy(Player p) => Checked.Toward < 0 ? p.GlobalPosition.X > doorX : p.GlobalPosition.X < doorX;

        StringName second = GateKinds.HintLine(Ability.MagGrip);
        Steps.Add(new("without the item, the pilgrim says what the door in sight needs, standing out of its range", 0.5f,
            () => Player.Respawn(before, Checked.Toward),
            () => _refused && Door is { } door && !door.Near(Player.GlobalPosition) && Hint.Current == door.Item.LockedLine));
        Steps.Add(new("a second line said meanwhile waits, and is read after it", 6f,
            () => Hint.Show(second, GateKinds.ColorOf(Ability.MagGrip)),
            () => Hint.Current == second && Hint.Waiting == 0));
        Steps.Add(new("walking into the door is stopped by it", 2f,
            () => Press(Toward),
            () => Ticks > 90 && Door is { Opened: false } && Shy(Player) && Mathf.Abs(Player.Velocity.X) < 0.1f));
        Steps.Add(new($"in the {Checked.ItemRoom}, walking up to the item takes it, for good and in the save", 4f,
            () =>
            {
                Release(Toward);
                Load(Checked.ItemRoom);
                ItemPickup? pickup = World.Room.All<ItemPickup>().FirstOrDefault(p => p.Item.Id == Key);
                Player.Respawn((pickup?.GlobalPosition ?? Vector3.Zero) + new Vector3(1.5f, 0f, 0f), -1);
                Press(InputActions.MoveLeft);
            },
            () => Player.Items.Holds(Key) && Player.InControl && World.Room.All<ItemPickup>().Where(p => p.Item.Id == Key).All(p => p.Taken)
                  && Session.SavedItems.Contains(Key) && World.Room.All<ItemPickup>().Where(p => p.Item.Id == Key).All(p => Session.Done(p.NameText()))));
        Steps.Add(new($"back in the {Checked.DoorRoom} holding it, coming near the door opens it", 3f,
            () =>
            {
                Release(InputActions.MoveLeft);
                Load(Checked.DoorRoom);
                Player.Respawn(before, Checked.Toward);
                Press(Toward);
            },
            () => _opened));
        Steps.Add(new($"and walking on, under the rising gate, crosses into the {Checked.Beyond}", 4f, () => { },
            () => World.RoomName == Checked.Beyond && Player.InControl));
        if (Checked.WalkBack)
        {
            Steps.Add(new("walking back out, the door stands open", 5f,
                () => { Release(Toward); Press(Back); },
                () => World.RoomName == Checked.DoorRoom && Door is { Opened: true } door && door.Gate.Raised));
        }

        Steps.Add(new($"and the {Checked.DoorRoom} loaded again finds it open, remembered", 1f,
            () => { Release(Toward); Release(Back); Load(Checked.DoorRoom); },
            () => Door is { Opened: true } door && door.Gate.Raised && Session.Done(Checked.DoorRoom, door.NameText())));
    }

    protected override string Tag => Checked.Tag;

    protected override string Detail =>
        $"in {World.RoomName} at {Player.GlobalPosition}, velocity {Player.Velocity}, holding {Player.Items.Held}, " +
        $"door {(Door is { } d ? d.Opened ? "open" : "shut" : "none")}";

    protected override string Outcome =>
        $"{StepCount} steps (the door's line on sight, a second line read in turn, stopped by the door, the {Key} taken and saved, the door opened, " +
        $"through to the {Checked.Beyond}, open behind, remembered)";
}
