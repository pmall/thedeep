using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace TheDeep;

/// <summary>
/// The ground rules' part of <see cref="RoomValidator"/>: checks every room's blocks and meshes against
/// <see cref="GroundRules"/> (small steps, seam intrusions, meshes crossing the play plane including spinning ones, low
/// ceilings and coplanar overlapping faces) and summarizes totals across the world.
/// </summary>
internal sealed class GroundCheck
{
    /// <summary>The ground rules' kinds of finding, in the order of <see cref="_counts"/>.</summary>
    private static readonly string[] Kinds = { "step", "intrusion", "open mesh", "headroom", "coplanar" };

    private readonly List<(string Room, int[] Counts)> _counts = new();

    /// <summary>
    /// Runs every ground rule on <paramref name="room"/>, adding one <c>GROUND &lt;Room&gt; &lt;kind&gt;:</c> line per finding to
    /// <paramref name="problems"/>. A seam frame's blocks (<see cref="SeamEnd"/>) and blocks marked <see cref="Block.Low"/> are low
    /// on purpose; machines' bodies at rest (an <see cref="AnimatableBody3D"/>'s boxes) fill the ground they rest in; swum water is no
    /// running line; pieces walked through by design (<see cref="IWalkedThrough"/>) are no open mesh.
    /// </summary>
    public void Check(Room room, string name, PlayerTuning tuning, List<string> problems)
    {
        int[] counts = new int[Kinds.Length];
        void Found(int kind, string what)
        {
            counts[kind]++;
            problems.Add($"GROUND {name} {Kinds[kind]}: {what}");
        }

        List<Block> blocks = room.Blocks().ToList();
        var solids = blocks.Select(RoomShapes.BoxOf).ToList();
        var ends = new List<SeamEnd>();
        var frame = blocks.Select(b => Ancestors(b).OfType<SeamEnd>().FirstOrDefault() is { } end ? (int?)Index(ends, end) : null).ToList();
        var low = new HashSet<int>(Enumerable.Range(0, blocks.Count).Where(i => blocks[i].Low || frame[i] is not null));
        float radius = tuning.BodyRadius;
        var bounds = new Box(room.BoundsMin.X, room.BoundsMin.Y, room.BoundsMax.X, room.BoundsMax.Y);
        foreach (LowCeiling c in GroundRules.Headroom(solids, low, tuning.Move.JumpHeight, tuning.Move.Height, radius, bounds))
        {
            Found(3, $"{blocks[c.Ground].NameText()}'s top at y {c.Y:0.##}, x {c.MinX:0.##}..{c.MaxX:0.##}: every place under a solid, {blocks[c.Ceiling].NameText()} " +
                     $"{c.Clearance:0.##} m over it, cutting the jump short (mark a passage low on purpose with Block.Low)");
        }

        var unwalked = room.All<WaterBody>().Where(w => w.Swim).Select(w => w.PlayRegion).Concat(Resting(room)).ToList();
        foreach (Seam seam in GroundRules.Seams(solids, tuning.Move.LedgeAssist, unwalked, bounds, blocks.Select(Front).ToList()))
        {
            string where = $"{blocks[seam.A].NameText()} and {blocks[seam.B].NameText()} at x {seam.X:0.##}, y {seam.Y:0.##}";
            Found(seam.Kind == SeamKind.Step ? 0 : 4, seam.Kind == SeamKind.Step
                ? $"{where}: a {seam.Size:0.##} m step on a walking surface (under the {tuning.Move.LedgeAssist} m ledge assist)"
                : $"{where}: overlapping, a face in one plane seen over {seam.Size:0.##} m or m² (z-fighting; trim one so they only touch)");
        }

        foreach ((int a, int b, Box o) in GroundRules.Intrusions(solids, frame))
        {
            Found(1, $"{room.PathTextTo(blocks[a])} and {room.PathTextTo(blocks[b])} overlap at x {o.MinX:0.##}..{o.MaxX:0.##}, y {o.MinY:0.##}..{o.MaxY:0.##}: " +
                     "a seam frame owns its column (docs/gameplay.md#seam-rule)");
        }

        foreach (MeshInstance3D mesh in room.Descendants<Node>().OfType<MeshInstance3D>().Where(m => m.Mesh is not null && m.IsVisibleInTree() && m is not LightShaft))
        {
            if (Ancestors(mesh).Any(n => n is CollisionObject3D or WaterBody or HeatColumn or SlagBody or IWalkedThrough) || mesh.Children().Any(n => n is CollisionObject3D))
            {
                continue;
            }

            (NVector3 min, NVector3 max) = Swept(mesh, out bool turns);
            if (min.Z >= -radius || max.Z <= radius)
            {
                continue; // wholly in front of or behind the pilgrim's body
            }

            var plane = new Box(min.X, min.Y, max.X, max.Y);
            string lies = turns ? "sweeps across the play plane as it turns" : "lies across the play plane";
            foreach ((float from, float to) in GroundRules.OpenSpans(plane, solids))
            {
                Found(2, $"{room.PathTextTo(mesh)} {lies} (y {plane.MinY:0.##}..{plane.MaxY:0.##}) with no solid from x {from:0.##} to {to:0.##}: the pilgrim passes through it");
            }
        }

        if (counts.Any(c => c > 0))
        {
            _counts.Add((name, counts));
        }
    }

    /// <summary>The ground rules' totals over the world, and the rooms with the most findings.</summary>
    public string Total()
    {
        string kinds = string.Join(", ", Kinds.Select((k, i) => $"{k} {_counts.Sum(r => r.Counts[i])}"));
        string most = string.Join(", ", _counts.OrderByDescending(r => r.Counts.Sum()).Take(5).Select(r => $"{r.Room} {r.Counts.Sum()}"));
        return $"GROUND REPORT: {kinds}; most: {most}";
    }

    /// <summary>The depth of a block's face toward the camera, or null when it draws no faces (<see cref="Block.CollisionOnly"/>).</summary>
    private static float? Front(Block b) => b.CollisionOnly ? null : b.GlobalPosition.Z + 0.5f * b.Size.Z;

    /// <summary>The world box a mesh fills, over a full turn of every <see cref="Spinner"/> it hangs from (<see cref="SpinSweep"/>).</summary>
    private static (NVector3 Min, NVector3 Max) Swept(MeshInstance3D mesh, out bool turns)
    {
        Aabb local = mesh.GetAabb();
        NVector3[] points = SpinSweep.Corners(local.Position.ToCore(), local.End.ToCore()).Select(c => (mesh.GlobalTransform * c.ToGodot()).ToCore()).ToArray();
        (NVector3 Min, NVector3 Max) box = (points.Aggregate(NVector3.Min), points.Aggregate(NVector3.Max));
        turns = false;
        foreach (Spinner spinner in Ancestors(mesh).OfType<Spinner>())
        {
            box = SpinSweep.Bounds(points, spinner.GlobalPosition.ToCore(), spinner.GlobalAxis.ToCore());
            points = SpinSweep.Corners(box.Min, box.Max);
            turns = true;
        }

        return box;
    }

    private static int Index<T>(List<T> list, T item)
    {
        int i = list.IndexOf(item);
        if (i < 0)
        {
            list.Add(item);
            i = list.Count - 1;
        }

        return i;
    }

    /// <summary>The play-plane boxes of the room's machines at rest: every box shape of an <see cref="AnimatableBody3D"/> where the scene places it.</summary>
    private static IEnumerable<Box> Resting(Room room) =>
        room.Descendants<Node>().OfType<CollisionShape3D>().Where(s => s.GetParent() is AnimatableBody3D && s.Shape is BoxShape3D)
            .Select(s => Box.Around(s.GlobalPosition.Flat(), new NVector2(((BoxShape3D)s.Shape).Size.X, ((BoxShape3D)s.Shape).Size.Y)));

    private static IEnumerable<Node> Ancestors(Node node)
    {
        for (Node? n = node.GetParent(); n is not null; n = n.GetParent())
        {
            yield return n;
        }
    }
}
