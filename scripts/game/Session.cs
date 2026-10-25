using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The game's flow around the pilgrim: resting at shrines (heal and save), taking ability relics, dying and
/// coming back at the last shrine rested at, and starting from the save. Holds the <see cref="SaveData"/>.
/// Machines come back whenever a room is entered again (it is a fresh instance); taken relics never do.
/// </summary>
public partial class Session : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public ScreenFade Fade { get; set; } = null!;
    [Export] public Banner Banner { get; set; } = null!;

    /// <summary>How close (meters, on the play plane) the pilgrim must stand to rest at a shrine or take a relic.</summary>
    [Export] public float RestRange { get; set; } = 1.6f;
    [Export] public float RelicRange { get; set; } = 1.2f;

    /// <summary>Dying: the veil falls this slowly, and lifts this slowly at the shrine.</summary>
    [Export] public float DeathFadeOut { get; set; } = 1.1f;
    [Export] public float DeathFadeIn { get; set; } = 0.9f;

    private bool _busy;
    private Shrine? _prompted;

    public SaveData Save { get; private set; } = new();

    /// <summary>False writes nothing to disk (checks and look-dev runs leave the owner's save alone).</summary>
    public bool Persist { get; set; } = true;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        Player.Died += OnDied;
        OnRoomLoaded(World.Room);
        if (Save.Room == "")
        {
            Save.Room = World.RoomName;
        }
    }

    public override void _ExitTree()
    {
        World.RoomLoaded -= OnRoomLoaded;
        Player.Died -= OnDied;
    }

    /// <summary>Starts from <paramref name="data"/> (the world already loaded its room): abilities, map, the pilgrim at its shrine.</summary>
    public void Resume(SaveData data)
    {
        Save = data;
        Player.Abilities.Set((Ability)data.Abilities, true);
        World.Map.Restore(data.MapRooms(), World.RoomName);
        if (World.Map.Find(World.RoomName) is null)
        {
            World.Enter(World.Room, System.Numerics.Vector2.Zero);
        }

        OnRoomLoaded(World.Room);
        Player.Respawn(RespawnPoint(World.Room), 1);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_busy || !Player.InControl)
        {
            Prompt(null);
            return;
        }

        Vector3 p = Player.GlobalPosition;
        Room room = World.Room;
        Shrine? near = Player.IsUpright && Player.IsOnFloor()
            ? room.All<Shrine>().FirstOrDefault(s => Flat(s.GlobalPosition).DistanceTo(Flat(p)) <= RestRange)
            : null;
        Prompt(near);
        if (near is not null && Input.IsActionJustPressed(InputActions.Interact))
        {
            Rest(near);
            return;
        }

        if (room.All<AbilityRelic>().FirstOrDefault(r => !r.Taken && Flat(r.GlobalPosition).DistanceTo(Flat(p)) <= RelicRange) is { } relic)
        {
            Take(relic);
        }
    }

    private void Prompt(Shrine? shrine)
    {
        if (_prompted == shrine)
        {
            return;
        }

        if (IsInstanceValid(_prompted))
        {
            _prompted!.ShowPrompt(false);
        }

        _prompted = shrine;
        shrine?.ShowPrompt(true);
    }

    /// <summary>Resting: full health, the shrine flares, and the game is saved here.</summary>
    private async void Rest(Shrine shrine)
    {
        _busy = true;
        Player.Vitality.Restore();
        Save.Room = World.RoomName;
        Save.Shrine = shrine.Name;
        Write();
        shrine.Flare();
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        await ToSignal(GetTree().CreateTimer(0.6f, processAlways: false), SceneTreeTimer.SignalName.Timeout);
        Player.Frozen = false;
        _busy = false;
    }

    /// <summary>Taking a relic: the pilgrim stands still while its core drifts in, then the ability is its own.</summary>
    private async void Take(AbilityRelic relic)
    {
        _busy = true;
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        await relic.Absorb(Player, new Vector3(0f, 1.05f, 0f));
        Player.Abilities.Unlock(relic.Ability);
        string id = SaveData.RelicId(World.RoomName, relic.Name);
        if (!Save.Collected.Contains(id))
        {
            Save.Collected.Add(id);
        }

        Write();
        Player.Frozen = false;
        _busy = false;
        await Banner.Show(relic.NameLine, relic.HintLine);
    }

    /// <summary>Death: the veil falls, the pilgrim wakes at the last shrine rested at, whole, the room renewed.</summary>
    private async void OnDied()
    {
        _busy = true;
        await Fade.To(1f, DeathFadeOut);
        Player.Frozen = true;
        string path = World.RoomPath(Save.Room);
        Room room = World.Load(GD.Load<PackedScene>(ResourceLoader.Exists(path) ? path : World.RoomPath(World.RoomName)));
        Player.Respawn(RespawnPoint(room), 1);
        Player.Vitality.Restore();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Player.Frozen = false;
        _busy = false;
        await Fade.To(0f, DeathFadeIn);
    }

    private Vector3 RespawnPoint(Room room) =>
        room.All<Shrine>().FirstOrDefault(s => s.Name == Save.Shrine)?.GlobalPosition ?? room.SpawnPoint.GlobalPosition;

    private void OnRoomLoaded(Room room)
    {
        Prompt(null);
        string name = World.NameOf(room.SceneFilePath);
        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            if (Save.Collected.Contains(SaveData.RelicId(name, relic.Name)))
            {
                relic.Empty();
            }
        }
    }

    private void Write()
    {
        Save.Abilities = (int)Player.Abilities.Unlocked;
        Save.SetMap(World.Map.Rooms);
        if (Persist)
        {
            SaveFile.Write(Save);
        }
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Y);
}
