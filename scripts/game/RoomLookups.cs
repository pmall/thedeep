using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// What the pilgrim stands at in a room each physics tick, written as loops over the room's kept lists: LINQ with a lambda
/// capturing the position allocates every tick (<c>instructions/principles.md#performance</c>).
/// </summary>
public static class RoomLookups
{
    /// <summary>The first of <paramref name="nodes"/> within <paramref name="range"/> of <paramref name="p"/> on the play plane, or null.</summary>
    public static T? Near<T>(IReadOnlyList<T> nodes, Vector3 p, float range)
        where T : Node3D
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].GlobalPosition.WithinOnPlane(p, range))
            {
                return nodes[i];
            }
        }

        return null;
    }

    /// <summary>The map goal area <paramref name="p"/> is in, or null.</summary>
    public static MapGoal? GoalAt(Room room, Vector3 p)
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

    /// <summary>Whether a boss seal of the room is still unwon.</summary>
    public static bool BossLeft(Room room)
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

    /// <summary>The proclamation <paramref name="p"/> stands in that is not among <paramref name="spoken"/>, or null.</summary>
    public static Proclamation? Unspoken(Room room, Vector3 p, HashSet<Proclamation> spoken)
    {
        IReadOnlyList<Proclamation> said = room.All<Proclamation>();
        for (int i = 0; i < said.Count; i++)
        {
            if (!spoken.Contains(said[i]) && said[i].Contains(p))
            {
                return said[i];
            }
        }

        return null;
    }
}
