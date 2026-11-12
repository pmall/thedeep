using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Reads a room scene into the core's <see cref="RoomShape"/>, what the planner and the map's proof see of it: its blocks as solids
/// (and the sound ones), its exits leading somewhere that exists (or sealed), where the pilgrim wakes, its anchors, lifts, swimmable
/// water, heat, slag, metal, and the areas walking into takes a relic, an item or pulls a lever (their reach from the feet) or reaches a
/// goal. The room must be in the tree at the origin, so global positions are room positions.
/// </summary>
public static class RoomShapes
{
    /// <summary>The shape of <paramref name="room"/>; <paramref name="start"/>: it is the world's start room (its spawn point is a wake).</summary>
    public static RoomShape Of(Room room, bool start)
    {
        var solids = room.Blocks().Select(BoxOf).ToList();
        var sound = room.Blocks().Where(b => !b.Cracked).Select(BoxOf).ToList();
        var exits = room.Exits().Where(e => e.TargetRoom == "" || ResourceLoader.Exists(e.TargetRoom))
            .Select(e => new ExitShape(e.Name, e.Threshold, e.Requires, e.ArrivalSide, e.TargetRoom == "")).ToList();
        var wakes = room.All<Shrine>().Select(s => s.GlobalPosition.Flat()).ToList();
        if (start)
        {
            wakes.Add(room.SpawnPoint.GlobalPosition.Flat());
        }

        var anchors = room.All<HarpoonAnchor>().Select(a => a.GlobalPosition.Flat()).ToList();
        var waters = room.All<WaterBody>().Where(w => w.Swim).Select(w => w.PlayRegion).ToList();
        var heat = room.All<HeatColumn>().Select(z => z.Region).ToList();
        // Where walking takes a relic (its range from the feet) and reaches a goal (the feet inside its area), as trigger boxes.
        var relics = room.All<AbilityRelic>()
            .Select(r => (Box.Around(r.GlobalPosition.Flat() + new NVector2(0f, 0.1f), new NVector2(1.6f, 0.4f)), r.Ability)).ToList();
        var items = room.All<ItemPickup>()
            .Select(i => (Box.Around(i.GlobalPosition.Flat() + new NVector2(0f, 0.1f), new NVector2(1.6f, 0.4f)), i.Item.Id))
            .Concat(room.All<Lever>().Select(l => (Box.Around(l.GlobalPosition.Flat() + new NVector2(0f, 0.1f), new NVector2(2f * l.Range, 0.4f)), l.Item.Id)))
            .ToList();
        var goals = room.All<MapGoal>().Where(g => g.Size != Vector2.Zero).Select(g => Box.Around(g.GlobalPosition.Flat(), g.Size.ToCore())).ToList();
        var metal = room.Blocks().Where(b => b.Surface == SurfaceKind.Metal && !b.Cracked).Select(BoxOf).ToList();
        return new RoomShape(solids, exits, wakes, anchors, room.All<Lift>().Select(l => l.Path).ToList(), sound, waters, heat, relics, goals, metal,
            room.SpawnPoint.GlobalPosition.Flat())
        {
            Items = items,
            Slag = room.All<SlagBody>().Select(s => s.PlayRegion).ToList(),
        };
    }

    /// <summary>A block as a box on the play plane.</summary>
    public static Box BoxOf(Block b) => Box.Around(b.GlobalPosition.Flat(), new NVector2(b.Size.X, b.Size.Y));
}
