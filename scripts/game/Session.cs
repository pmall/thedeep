using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The game's flow around the pilgrim: resting at shrines (heal and save), taking ability relics (items and their doors are the
/// <see cref="Keyring"/>'s), reading
/// scripture plates, lines spoken to it (<see cref="Proclamation"/>), law breaks
/// (<see cref="LawBreak"/>), lifts (<see cref="Lift"/>) and cracked rock broken by the Drill (it stays broken), burned by heat
/// and set back on its footing, dying or falling out of a room and coming back at the last shrine rested at, the end of what is built
/// (<see cref="MapGoal"/> areas), and starting from the save. The progress and its rules are the core's
/// (<see cref="Pilgrimage"/>: what is done for good, the last rest, a visit's moments); this node finds them in the room and plays
/// them. Machines come back whenever a room is entered again (it is a fresh instance); taken relics, beaten bosses and broken laws
/// never do.
/// </summary>
public partial class Session : Node
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public ScreenFade Fade { get; set; } = null!;
    [Export] public Banner Banner { get; set; } = null!;
    [Export] public SideCamera Camera { get; set; } = null!;
    [Export] public EndCard EndCard { get; set; } = null!;
    [Export] public RoomTravel Travel { get; set; } = null!;

    /// <summary>How close (meters, on the play plane) the pilgrim must stand to rest at a shrine or take a relic.</summary>
    [Export] public float RestRange { get; set; } = 1.6f;
    [Export] public float RelicRange { get; set; } = 1.2f;
    [Export] public float ReadRange { get; set; } = 1.2f;

    /// <summary>Falling this far below a room's camera bounds is falling out of it: back to the last shrine, meters.</summary>
    [Export] public float FallOutDepth { get; set; } = 3f;

    /// <summary>Dying: the veil falls this slowly, and lifts this slowly at the shrine.</summary>
    [Export] public float DeathFadeOut { get; set; } = 1.1f;

    /// <summary>How long resting at a shrine holds the pilgrim, down on one knee (seconds).</summary>
    [Export] public float RestTime { get; set; } = 0.9f;
    [Export] public float DeathFadeIn { get; set; } = 0.9f;

    /// <summary>Burned by heat: the veil falls this fast over the pilgrim's recoil, and lifts this fast on its footing (seconds).</summary>
    [Export] public float ScorchFadeOut { get; set; } = 0.2f;
    [Export] public float ScorchFadeIn { get; set; } = 0.3f;

    private bool _busy;
    private Shrine? _prompted;
    private ScripturePlate? _plate;

    /// <summary>The proclamations heard this visit (each speaks once per visit).</summary>
    private readonly HashSet<Proclamation> _spoken = new();

    private Pilgrimage _pilgrimage = new(new SaveData());

    public SaveData Save => _pilgrimage.Save;

    /// <summary>A shrine or a plate is offered (interact would rest or read): nothing else takes the press.</summary>
    public bool Prompting => _prompted is not null || _plate is not null;

    /// <summary>Raised when a <see cref="Proclamation"/> speaks to the pilgrim.</summary>
    public event System.Action<Proclamation>? Spoke;

    /// <summary>False writes nothing to disk (checks and look-dev runs leave the owner's save alone).</summary>
    public bool Persist { get; set; } = true;

    public override void _Ready()
    {
        World.RoomLoaded += OnRoomLoaded;
        Player.Died += OnDied;
        Player.Scorched += SetBack;
        OnRoomLoaded(World.Room);
    }

    public override void _ExitTree()
    {
        World.RoomLoaded -= OnRoomLoaded;
        Player.Died -= OnDied;
        Player.Scorched -= SetBack;
    }

    /// <summary>Starts from <paramref name="data"/> (the world already loaded its room): abilities, map, the pilgrim at its shrine.</summary>
    public void Resume(SaveData data)
    {
        _pilgrimage = new Pilgrimage(data);
        Player.Abilities.Set((Ability)data.Abilities, true);
        Player.Items.Restore(_pilgrimage.Held);
        Player.Strengthen(_pilgrimage.Prowess);
        World.Map.Restore(data.MapRooms(), World.RoomName, data.MapWays(), data.Taken);
        // Entered again where the save placed it (or at the origin): its ways out are read afresh from the room.
        World.Enter(World.Room, World.Map.Find(World.RoomName)?.Offset ?? System.Numerics.Vector2.Zero);

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
        if (Pilgrimage.FellOut(p.Y, room.BoundsMin.Y, FallOutDepth))
        {
            FellOut();
            return;
        }

        if (GoalAt(room, p) is { } goal)
        {
            switch (_pilgrimage.ReachGoal(goal.Ending, goal.Ending && BossLeft(room)))
            {
                case GoalMoment.End:
                    End();
                    return;
                case GoalMoment.Shown:
                    _ = Banner.Show(goal.TitleLine, goal.HintLine, 5f);
                    break;
                case GoalMoment.None:
                    break;
            }
        }

        if (Unspoken(room, p) is { } spoken)
        {
            _spoken.Add(spoken);
            _ = Banner.Show(spoken.SpeakerLine, spoken.TextLine, 5f);
            Spoke?.Invoke(spoken);
        }

        bool standing = Player.IsUpright && Player.IsOnFloor();
        Shrine? near = standing ? Near(room.All<Shrine>(), p, RestRange) : null;
        Prompt(near);
        ScripturePlate? plate = standing && near is null
            ? Near(room.All<ScripturePlate>(), p, ReadRange)
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

        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            if (!relic.Taken && Flat(relic.GlobalPosition).DistanceTo(Flat(p)) <= RelicRange)
            {
                Take(relic);
                break;
            }
        }
    }

    // Per-tick lookups, written as loops over the room's kept lists: LINQ with a lambda capturing the position allocates every tick.

    private static T? Near<T>(IReadOnlyList<T> nodes, Vector3 p, float range) where T : Node3D
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (Flat(nodes[i].GlobalPosition).DistanceTo(Flat(p)) <= range)
            {
                return nodes[i];
            }
        }

        return null;
    }

    private static MapGoal? GoalAt(Room room, Vector3 p)
    {
        IReadOnlyList<MapGoal> goals = room.All<MapGoal>();
        for (int i = 0; i < goals.Count; i++)
        {
            if (goals[i].Contains(p))
            {
                return goals[i];
            }
        }

        return null;
    }

    private static bool BossLeft(Room room)
    {
        IReadOnlyList<BossSeal> seals = room.All<BossSeal>();
        for (int i = 0; i < seals.Count; i++)
        {
            if (!seals[i].Won)
            {
                return true;
            }
        }

        return false;
    }

    private Proclamation? Unspoken(Room room, Vector3 p)
    {
        IReadOnlyList<Proclamation> said = room.All<Proclamation>();
        for (int i = 0; i < said.Count; i++)
        {
            if (!_spoken.Contains(said[i]) && said[i].Contains(p))
            {
                return said[i];
            }
        }

        return null;
    }

    private void Prompt(Shrine? shrine)
    {
        if (_prompted == shrine)
        {
            return;
        }

        if (_prompted is { } shown && IsInstanceValid(shown))
        {
            shown.ShowPrompt(false);
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

        if (_plate is { } shown && IsInstanceValid(shown))
        {
            shown.ShowPrompt(false);
        }

        _plate = plate;
        plate?.ShowPrompt(true);
    }

    /// <summary>Resting: full health, the shrine flares, and the game is saved here.</summary>
    private async void Rest(Shrine shrine)
    {
        _busy = true;
        Player.Vitality.Restore();
        _pilgrimage.Rest(World.RoomName, shrine.Name);
        Write();
        shrine.Flare();
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        Player.Visual.Moment = PilgrimMoment.Rest;
        await ToSignal(GetTree().CreateTimer(RestTime, processAlways: false), SceneTreeTimer.SignalName.Timeout);
        Player.Visual.Moment = PilgrimMoment.None;
        Player.Frozen = false;
        _busy = false;
    }

    /// <summary>Taking a relic: the pilgrim stands still while its core drifts in, then the ability is its own.</summary>
    private async void Take(AbilityRelic relic)
    {
        _busy = true;
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        Player.Visual.Moment = PilgrimMoment.Relic;
        await relic.Absorb(Player, new Vector3(0f, 1.05f, 0f));
        Player.Visual.Moment = PilgrimMoment.None;
        Player.Abilities.Unlock(relic.Ability);
        Remember(relic.Name);
        Player.Frozen = false;
        _busy = false;
        await Banner.Show(relic.NameLine, relic.HintLine);
    }

    /// <summary>
    /// The end of the game (the Heart): the pilgrim stands still, the end card plays, then a new game begins (the save, if
    /// this run keeps one, is gone: the next pilgrimage starts at the world's start room).
    /// </summary>
    private async void End()
    {
        _busy = true;
        Player.Velocity = Vector3.Zero;
        Player.Frozen = true;
        Ended?.Invoke();
        await EndCard.Play();
        if (Persist)
        {
            SaveFile.Delete(World.Definition.SavePath);
        }

        GetTree().ReloadCurrentScene();
    }

    /// <summary>Raised when the pilgrim reaches the end of the game, as the end card begins.</summary>
    public event System.Action? Ended;

    /// <summary>Death: the veil falls, the pilgrim wakes at the last shrine rested at, whole, the room renewed.</summary>
    private void OnDied()
    {
        GD.Print($"[died] in {World.RoomName} at {Player.GlobalPosition}");
        Player.Visual.Moment = PilgrimMoment.Death;
        Return();
    }

    /// <summary>
    /// Fallen out of the room (below its camera bounds): a hole the room check missed, so it is logged as an error. The pilgrim is put
    /// back at the exit it came in by (<see cref="RoomTravel.Return"/>), or, if it woke in this room, returned to its shrine like a
    /// death, health untouched by the fall. With travel off (look-dev's scripted runs), walking out through an exit falls out too: that
    /// is no hole, so it is only printed.
    /// </summary>
    private void FellOut()
    {
        string report = $"[fell out] of {World.RoomName} at {Player.GlobalPosition}, moving {Player.Velocity}, {Player.CurrentState.GetType().Name}";
        if (Travel.Enabled)
        {
            GD.PushError(report);
        }
        else
        {
            GD.Print(report + " (travel off)");
        }

        Player.Velocity = Vector3.Zero;
        if (!Travel.Return())
        {
            Return();
        }
    }

    /// <summary>
    /// Burned by heat without its plating (`docs/gameplay.md#heat`): the veil falls quickly over the recoil, the pilgrim stands
    /// again on its <see cref="Player.Footing"/>, in control, and the veil lifts.
    /// </summary>
    private async void SetBack()
    {
        _busy = true;
        Player.Frozen = true;
        await Fade.To(1f, ScorchFadeOut);
        Player.Respawn(Player.Footing);
        Player.Frozen = false;
        _busy = false;
        await Fade.To(0f, ScorchFadeIn);
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
        _spoken.Clear();
        string name = World.NameOf(room.SceneFilePath);
        _pilgrimage.Enter(name);
        foreach (AbilityRelic relic in room.All<AbilityRelic>())
        {
            if (_pilgrimage.RelicEmpty(name, relic.Name, relic.Ability, Player.Abilities.Unlocked))
            {
                relic.Empty();
            }
        }

        foreach (Block block in room.Blocks().Where(b => b.Cracked))
        {
            if (_pilgrimage.Done(name, block.Name))
            {
                block.Settle();
                continue;
            }

            block.Shattered += OnShattered;
        }

        foreach (HeatZone zone in room.All<HeatZone>())
        {
            zone.Player = Player;
        }

        foreach (Lift lift in room.All<Lift>())
        {
            lift.Player = Player;
            lift.Released += _ => Camera.AddShake(0.12f);
        }

        foreach (LawBreak law in room.All<LawBreak>())
        {
            law.Player = Player;
            if (_pilgrimage.Done(name, law.Name))
            {
                law.Settle();
                continue;
            }

            law.Broke += OnLawBroke;
        }
    }

    /// <summary>Cracked rock broken by the Drill: remembered (and saved) at once, so the way stays open.</summary>
    private void OnShattered(Block block)
    {
        Remember(block.Name);
        Camera.AddShake(0.25f);
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

    /// <summary>Whether the node <paramref name="node"/> of <paramref name="room"/> is done for good (taken, beaten, broken, opened).</summary>
    public bool Done(string room, string node) => _pilgrimage.Done(room, node);

    /// <summary>Whether the node <paramref name="node"/> of the current room is done for good.</summary>
    public bool Done(string node) => _pilgrimage.Done(World.RoomName, node);

    /// <summary>Whether an item pickup lies taken (<see cref="Pilgrimage.ItemTaken"/>).</summary>
    public bool ItemTaken(string room, string node, string item, ItemSet held) => _pilgrimage.ItemTaken(room, node, item, held);

    /// <summary>
    /// Done for good in the current room (a relic or an item taken, a boss beaten, a law broken, a door opened): kept in the save,
    /// written at once.
    /// </summary>
    public void Remember(string node)
    {
        _pilgrimage.Remember(World.RoomName, node);
        Write();
    }

    private void Write()
    {
        _pilgrimage.Record(Player.Abilities.Unlocked, Player.Items.Held, Player.Prowess, World.Map);
        if (Persist)
        {
            SaveFile.Write(World.Definition.SavePath, Save);
        }
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Y);
}
