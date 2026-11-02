using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// The run's progress and its rules, pure: what is done for good (relics and items taken, bosses beaten, laws broken, cracked rock bored,
/// locked doors opened, by room and node), the shrine last rested at, what the save keeps, and the moments of a visit to a room (the end of what is built is
/// shown once per visit; the end of the game only once its room's boss is broken). The session node applies it to the rooms.
/// </summary>
public sealed class Pilgrimage
{
    private bool _goalShown;

    public Pilgrimage(SaveData save) => Save = save;

    /// <summary>What the save holds; written out by the session whenever it changes.</summary>
    public SaveData Save { get; }

    /// <summary>A visit to <paramref name="room"/> begins (a room loaded): its moments can happen again.</summary>
    public void Enter(string room)
    {
        _goalShown = false;
        if (Save.Room == "")
        {
            Save.Room = room; // where a new run starts, until the first rest
        }
    }

    /// <summary>Whether the node <paramref name="node"/> of <paramref name="room"/> is done for good.</summary>
    public bool Done(string room, string node) => Save.Collected.Contains(SaveData.RelicId(room, node));

    /// <summary>Marks a node done for good; returns false if it already was.</summary>
    public bool Remember(string room, string node)
    {
        string id = SaveData.RelicId(room, node);
        if (Save.Collected.Contains(id))
        {
            return false;
        }

        Save.Collected.Add(id);
        return true;
    }

    /// <summary>Resting at <paramref name="shrine"/> in <paramref name="room"/>: where the pilgrim comes back from now on.</summary>
    public void Rest(string room, string shrine)
    {
        Save.Room = room;
        Save.Shrine = shrine;
    }

    /// <summary>A relic lies empty when it was taken, or when its ability is already owned (a room tried with its route's abilities).</summary>
    public bool RelicEmpty(string room, string relic, Ability ability, Ability owned) => Done(room, relic) || (owned & ability) != 0;

    /// <summary>What the save keeps of the pilgrim and the map, before it is written.</summary>
    public void Record(Ability owned, ItemSet held, MapState map)
    {
        Save.Abilities = (int)owned;
        Save.Items = new List<string>(held.Ids);
        Save.SetMap(map);
    }

    /// <summary>The items the save holds.</summary>
    public ItemSet Held => ItemSet.Of(Save.Items);

    /// <summary>An item lies taken when it was, or when it is already held (a room tried with its route's items).</summary>
    public bool ItemTaken(string room, string node, string item, ItemSet held) => Done(room, node) || held.Contains(item);

    /// <summary>
    /// The pilgrim reached a goal: what happens, once per visit. The end of the game ends the run, once its room's boss is broken
    /// (<paramref name="bossLeft"/> false; until then nothing happens and it can still be reached); any other goal is shown.
    /// </summary>
    public GoalMoment ReachGoal(bool ending, bool bossLeft)
    {
        if (_goalShown || (ending && bossLeft))
        {
            return GoalMoment.None;
        }

        _goalShown = true;
        return ending ? GoalMoment.End : GoalMoment.Shown;
    }

    /// <summary>Whether feet at <paramref name="y"/> fell <paramref name="depth"/> meters below a room's bottom (<paramref name="floor"/>).</summary>
    public static bool FellOut(float y, float floor, float depth) => y < floor - depth;
}

/// <summary>What reaching a goal does (<see cref="Pilgrimage.ReachGoal"/>).</summary>
public enum GoalMoment
{
    None,

    /// <summary>The end of what is built: its banner is shown.</summary>
    Shown,

    /// <summary>The end of the game: the end card, then a new game.</summary>
    End,
}
