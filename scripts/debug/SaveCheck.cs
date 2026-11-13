using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The save, end to end, in two fresh runs (run by tools/check.sh) on a scratch save that is never the owner's
/// (<see cref="ScratchPrefix"/>; `--save-name`). The write run (`-- --room Threshold --save-name savecheck --save-check-write`) gives the
/// pilgrim a state unlike a new game's in every saved field (abilities, an item, power-ups, something done for good, a map of two
/// rooms, a way taken) with the debug hooks, then rests at the Threshold's shrine through the real Interact action, which saves by the
/// game's own path. The read run (`-- --save-name savecheck --save-check-read`) is a normal launch: Main reads that file and
/// <see cref="Session.Resume"/> restores it; the check asserts every field came back, that a save that cannot be read is set aside and
/// never overwritten (<see cref="SaveFile.SetAside"/>), and removes its scratch files. Prints `SAVE WRITTEN` then `SAVE OK`, or `SAVE CHECK` errors.
/// </summary>
public partial class SaveCheck : Node
{
    /// <summary>Every scratch save name starts with this; a run on another name is refused, so no check touches a real save.</summary>
    public const string ScratchPrefix = "savecheck";

    /// <summary>Whether this run writes the save (the first run) or reads it (the second).</summary>
    public required bool Write { get; init; }
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world.</summary>
    public required World World { get; init; }
    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }

    private const Ability Owned = Ability.MagGrip | Ability.Harpoon;
    private const string Item = "SacristyKey";
    private const string Done = "CheckDeed";
    private const string Start = "Threshold";
    private const string Other = "Narthex";
    private static readonly Prowess Gained = new(2, 3);

    private readonly List<string> _problems = new();
    private int _tick;
    private int _rested = -1;

    /// <summary>Runs before the pilgrim each tick, so what it presses is read the same tick.</summary>
    public SaveCheck()
    {
        ProcessPhysicsPriority = -100;
    }

    private string Path => World.Definition.SavePath;

    public override void _Ready()
    {
        if (!World.Definition.SaveName.StartsWith(ScratchPrefix, System.StringComparison.Ordinal))
        {
            _problems.Add($"the save name {World.Definition.SaveName} is not a scratch name (--save-name {ScratchPrefix}...): refused");
            Finish();
            return;
        }

        if (Write)
        {
            Clean();
            Player.Abilities.Set(Owned, true);
            Player.Items.Restore(ItemSet.Of(new[] { Item }));
            Player.Strengthen(Gained);
            Session.Remember(Done);
            World.Load(GD.Load<PackedScene>(World.RoomPath(Other)));
        }
        else
        {
            AssertResumed();
            AssertSetAside();
            Clean();
            Finish();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Write || _problems.Count > 0)
        {
            return;
        }

        _tick++;
        switch (_tick)
        {
            case 10:
                World.Load(GD.Load<PackedScene>(World.RoomPath(Start)));
                World.Map.Take(Start, World.Room.Exits().First().Name);
                break;
            case 20:
                Shrine? shrine = World.Room.First<Shrine>();
                if (shrine is null)
                {
                    _problems.Add($"{Start} has no shrine");
                    Finish();
                    return;
                }

                Player.Respawn(shrine.GlobalPosition, 1);
                break;
            case 60:
                Input.ActionPress(InputActions.Interact);
                break;
            case 62:
                Input.ActionRelease(InputActions.Interact);
                break;
            case 200:
                if (Session.Save.Shrine == "" || !FileAccess.FileExists(Path))
                {
                    _problems.Add($"resting at the shrine did not save: shrine '{Session.Save.Shrine}', file {(FileAccess.FileExists(Path) ? "there" : "missing")}");
                }

                Finish();
                break;
        }
    }

    private void AssertResumed()
    {
        SaveData? file = SaveFile.Read(Path);
        if (file is null)
        {
            _problems.Add($"the write run left no readable save at {Path}");
            return;
        }

        Expect("the file's room is where it rested", file.Room, Start);
        Expect("the file's shrine", file.Shrine, World.Room.First<Shrine>()?.Name.ToString() ?? "?");
        Expect("the room resumed in", World.RoomName, file.Room);
        Expect("abilities", Player.Abilities.Unlocked, Owned);
        Expect("items", string.Join(",", Player.Items.Held.Ids), Item);
        Expect("power-ups", Player.Prowess, Gained);
        Expect("the saved thing done for good is done again", Session.Done(Done), true);
        Expect("the file's collected", string.Join(",", file.Collected), SaveData.RelicId(Start, Done));
        Expect("the shrine is where the pilgrim stands", World.Room.First<Shrine>() is { } s && s.GlobalPosition.DistanceTo(Player.GlobalPosition) < 1.0f, true);
        Expect("the map's rooms", Names(World.Map.Rooms.Select(r => r.Name)), Names(file.MapRooms().Select(r => r.Name)));
        Expect("the map kept both rooms visited", Names(World.Map.Rooms.Select(r => r.Name)), Names(new[] { Start, Other }));
        Expect("the map's ways", World.Map.Ways.Count(), file.Ways.Count);
        Expect("the map has ways", file.Ways.Count > 0, true);
        Expect("the map's ways taken", Names(World.Map.TakenWays), Names(file.Taken));
        Expect("a way taken is kept", file.Taken.Count, 1);
        Expect("the session's save is the file's", Session.Save.ToJson(), file.ToJson());
    }

    /// <summary>A save that cannot be read is renamed aside, whole, so the file is never lost to the new game that follows.</summary>
    private void AssertSetAside()
    {
        const string junk = "{ this is not a save";
        using (FileAccess? file = FileAccess.Open(Path, FileAccess.ModeFlags.Write))
        {
            file?.StoreString(junk);
        }

        string? aside = SaveFile.SetAside(Path);
        Expect<string?>("the unreadable save is set aside", aside, SaveData.AsidePath(Path));
        Expect("nothing is left under the save's own name", FileAccess.FileExists(Path), false);
        Expect("the set-aside file keeps the text", aside is not null && FileAccess.FileExists(aside) ? FileAccess.GetFileAsString(aside) : "", junk);
    }

    private static string Names(IEnumerable<string> names) => string.Join(",", names.OrderBy(n => n, System.StringComparer.Ordinal));

    private void Expect<T>(string what, T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _problems.Add($"{what}: got {actual}, expected {expected}");
        }
    }

    /// <summary>Removes the scratch save and what it leaves beside it.</summary>
    private void Clean()
    {
        foreach (string path in new[] { Path, SaveData.TempPath(Path), SaveData.AsidePath(Path) })
        {
            SaveFile.Delete(path);
        }
    }

    private void Finish()
    {
        Input.ActionRelease(InputActions.Interact);
        foreach (string problem in _problems)
        {
            GD.PushError($"SAVE CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print(Write ? "SAVE WRITTEN: rested at the shrine, saved" : "SAVE OK: every field came back through the resume path; an unreadable save is set aside");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
