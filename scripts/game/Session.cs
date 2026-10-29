using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The game's flow around the pilgrim: resting at shrines (heal and save), taking ability relics, reading
/// scripture plates, boss fights (<see cref="BossSeal"/>) and law breaks (<see cref="LawBreak"/>), dying or
/// falling out of a room and coming back at the last shrine rested at, the end of what is built
/// (<see cref="MapGoal"/> areas), and starting from the save. Holds the <see cref="SaveData"/>. Machines come back
/// whenever a room is entered again (it is a fresh instance); taken relics, beaten bosses and broken laws never do.
/// </summary>
public partial class Session : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public ScreenFade Fade { get; set; } = null!;
    [Export] public Banner Banner { get; set; } = null!;
    [Export] public SideCamera Camera { get; set; } = null!;

    /// <summary>How close (meters, on the play plane) the pilgrim must stand to rest at a shrine or take a relic.</summary>
    [Export] public float RestRange { get; set; } = 1.6f;
    [Export] public float RelicRange { get; set; } = 1.2f;
    [Export] public float ReadRange { get; set; } = 1.2f;

    /// <summary>Falling this far below a room's camera bounds is falling out of it: back to the last shrine, meters.</summary>
    [Export] public float FallOutDepth { get; set; } = 3f;

    /// <summary>Dying: the veil falls this slowly, and lifts this slowly at the shrine.</summary>
    [Export] public float DeathFadeOut { get; set; } = 1.1f;
    [Export] public float DeathFadeIn { get; set; } = 0.9f;

    private bool _busy;
    private Shrine? _prompted;
    private ScripturePlate? _plate;
    private bool _goalShown;

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
            PromptPlate(null);
            return;
        }

        Vector3 p = Player.GlobalPosition;
        Room room = World.Room;
        if (p.Y < room.BoundsMin.Y - FallOutDepth)
        {
            FellOut();
            return;
        }

        if (!_goalShown && room.All<MapGoal>().FirstOrDefault(g => g.Contains(p)) is { } goal)
        {
            _goalShown = true;
            _ = Banner.Show(goal.TitleLine, goal.HintLine, 5f);
        }

        bool standing = Player.IsUpright && Player.IsOnFloor();
        Shrine? near = standing ? room.All<Shrine>().FirstOrDefault(s => Flat(s.GlobalPosition).DistanceTo(Flat(p)) <= RestRange) : null;
        Prompt(near);
        ScripturePlate? plate = standing && near is null
            ? room.All<ScripturePlate>().FirstOrDefault(s => Flat(s.GlobalPosition).DistanceTo(Flat(p)) <= ReadRange)
            : null;
        PromptPlate(plate);
        if (near is not null && Input.IsActionJustPressed(InputActions.Interact))
        {
            Rest(near);
            return;
        }

        if (plate is not null && Input.IsActionJustPressed(InputActions.Interact))
        {
            plate.Read();
            _ = Banner.Show(plate.TitleLine, plate.TextLine, 6f);
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

    private void PromptPlate(ScripturePlate? plate)
    {
        if (_plate == plate)
        {
            return;
        }

        if (IsInstanceValid(_plate))
        {
            _plate!.ShowPrompt(false);
        }

        _plate = plate;
        plate?.ShowPrompt(true);
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
        Remember(relic.Name);
        Player.Frozen = false;
        _busy = false;
        await Banner.Show(relic.NameLine, relic.HintLine);
    }

    /// <summary>Death: the veil falls, the pilgrim wakes at the last shrine rested at, whole, the room renewed.</summary>
    private void OnDied() => Return();

    /// <summary>
    /// Fallen out of the room (below its camera bounds: a drop into a room not built yet, like the Brink's): the same
    /// return as dying, health untouched by the fall.
    /// </summary>
    private void FellOut()
    {
        Player.Velocity = Vector3.Zero;
        Return();
    }

    private async void Return()
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
        PromptPlate(null);
        _goalShown = false;
        string name = World.NameOf(room.SceneFilePath);
        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            if (Save.Collected.Contains(SaveData.RelicId(name, relic.Name)))
            {
                relic.Empty();
            }
        }

        foreach (BossSeal seal in room.All<BossSeal>())
        {
            seal.Player = Player;
            if (Save.Collected.Contains(SaveData.RelicId(name, seal.Name)))
            {
                seal.Settle();
                continue;
            }

            seal.Began += OnBossBegan;
            seal.Defeated += OnBossDefeated;
            foreach (Portcullis gate in seal.Gates)
            {
                gate.Slammed += _ => Camera.AddShake(0.3f);
            }
        }

        foreach (LawBreak law in room.All<LawBreak>())
        {
            law.Player = Player;
            if (Save.Collected.Contains(SaveData.RelicId(name, law.Name)))
            {
                law.Settle();
                continue;
            }

            law.Broke += OnLawBroke;
        }
    }

    private void OnBossBegan(BossSeal seal) => _ = Banner.Show(seal.NameLine, "", 2.5f);

    /// <summary>A boss broken: remembered (and saved) at once, so it never stands again.</summary>
    private void OnBossDefeated(BossSeal seal)
    {
        Remember(seal.Name);
        Camera.AddShake(0.5f);
        _ = Banner.Show(seal.FallenLine, seal.FallenHintLine, 4f);
    }

    /// <summary>A law broken: remembered (and saved) at once; the moment is the pilgrim's, so it holds still a breath.</summary>
    private async void OnLawBroke(LawBreak law)
    {
        Remember(law.Name);
        Camera.AddShake(0.6f);
        _busy = true;
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        await ToSignal(GetTree().CreateTimer(0.9f, processAlways: false), SceneTreeTimer.SignalName.Timeout);
        Player.Frozen = false;
        _busy = false;
        await Banner.Show(law.BrokenLine, law.BrokenHintLine, 4f);
    }

    /// <summary>Done for good (a relic taken, a boss beaten, a law broken): kept in the save, written at once.</summary>
    private void Remember(string node)
    {
        string id = SaveData.RelicId(World.RoomName, node);
        if (!Save.Collected.Contains(id))
        {
            Save.Collected.Add(id);
        }

        Write();
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
