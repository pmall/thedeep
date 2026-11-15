using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// What the world check needs of one room, on its play plane (room positions): its solids (and the sound ones, without cracked
/// rock), its exits, where the pilgrim can wake (its shrines, and its spawn point in the start room), its anchors, lifts, swimmable
/// water, heat, slag, relics and items (the box walking into takes one) and goals, its metal, and its spawn point.
/// </summary>
public sealed record RoomShape(
    List<Box> Solids, List<ExitShape> Exits, List<Vector2> Wakes, List<Vector2> Anchors, List<LiftPath> Lifts, List<Box> Sound,
    List<Box> Waters, List<Box> Heat, List<(Box Area, Ability Ability)> Relics, List<Box> Goals, List<Box> Metal, Vector2 Spawn)
{
    /// <summary>The items lying in the room (keys, codes), by id, with the box walking into takes each.</summary>
    public List<(Box Area, string Item)> Items { get; init; } = new();

    /// <summary>Slag (`docs/gameplay.md#slag`): space no planner enters, plated or not; a way over it is a way over its surface.</summary>
    public List<Box> Slag { get; init; } = new();
}

