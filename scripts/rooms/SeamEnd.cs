using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// One room's end of a seam, framed from its <see cref="Profile"/> (<see cref="SeamFrame"/> has the rule): placed on a
/// <see cref="RoomExit"/> (as its child, where it stays), it sizes the exit and builds the frame around the opening as
/// <see cref="Block"/>s. The room at the other end frames its exit with the same profile, so the seam agrees by construction
/// (docs/gameplay.md#seam-rule): neither room places a block of its own there, and its own solids stop where the frame starts. The
/// frame's blocks take the look of the room's wall they meet; a doorway's frame is walled over its lintel and under its sill out to the
/// room's own solids (<see cref="SeamFrame.Wall"/>), so it reads as an opening cut in a wall, and one quiet line of trim (proud of the
/// front face, visual only) marks it. A
/// <see cref="Doorway"/> frames a west or east exit, a <see cref="Hatch"/> a down or up one.
/// </summary>
public abstract partial class SeamEnd : Node3D
{
    /// <summary>The kind of seam, the same at both ends of the way.</summary>
    [Export] public SeamProfile Profile { get; set; } = null!;

    /// <summary>What the frame is made of (its physics layer and default material, as a <see cref="Block"/>'s).</summary>
    [Export] public SurfaceKind Surface { get; set; } = SurfaceKind.Rock;

    /// <summary>Optional look replacing the default material of <see cref="Surface"/>.</summary>
    [Export] public Material? Material { get; set; }

    /// <summary>
    /// Whether the way is flooded: water runs through the opening from one room into the other. Set on both ends; each asks its room's
    /// swum <see cref="WaterBody"/> to fill the passage out to the frame's outer face and draw it as water there
    /// (<see cref="WaterBody.Flood"/>), so the water never ends at the seam (docs/gameplay.md#seam-rule).
    /// </summary>
    [Export] public bool Flooded { get; set; }

    /// <summary>How far a through piece of trim runs into the frame past each face, meters: never sharing a face's plane with it.</summary>
    private const float TrimOverlap = 0.04f;

    /// <summary>The most a trim piece's edges are bevelled, meters (a thin piece takes a quarter of its thinnest side: <see cref="TrimBevelShare"/>).</summary>
    private const float TrimBevel = 0.04f;

    /// <summary>A trim piece's bevel is at most this share of its thinnest side.</summary>
    private const float TrimBevelShare = 0.25f;

    /// <summary>A trim piece whose top is this near a water surface counts as lying on it.</summary>
    private const float WaterTouch = 0.1f;

    /// <summary>How far such a piece is lowered, meters: more than <see cref="WaterTouch"/>, so its top is clear of the surface.</summary>
    private const float WaterLower = 0.15f;

    /// <summary>
    /// How much deeper than the frame a water sheet through it is, meters: it reaches past both faces, so no seam of the frame
    /// shows against the water.
    /// </summary>
    protected const float WaterSheetExtra = 0.1f;

    /// <summary>The block the frame is built of (<c>scenes/kit/Block.tscn</c>).</summary>
    [Export] public PackedScene BlockScene { get; set; } = null!;

    /// <summary>Whether this kind of end frames an exit through a slab (a hatch) rather than a wall (a doorway).</summary>
    protected abstract bool FramesHatch { get; }

    /// <summary>The names of the frame's blocks, in the order <see cref="SeamFrame.Solids"/> gives them.</summary>
    protected abstract string[] PieceNames { get; }

    public override void _Ready()
    {
        var exit = GetParent() as RoomExit ?? throw new InvalidOperationException($"{GetType().Name} {this.NameText()} must be the child of a RoomExit");
        if (exit.Threshold.IsHatch != FramesHatch)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} {this.NameText()} is on {exit.NameText()}, a {(exit.Threshold.IsHatch ? "hatch" : "doorway")}: it frames the other kind");
        }

        SeamFrame frame = Profile.Frame;
        exit.Size = frame.ExitSize(exit.Side).ToGodot();
        Transform = Transform3D.Identity; // centered on its exit by construction, whatever the scene says

        var solids = frame.Solids(exit.Side, System.Numerics.Vector2.Zero);
        var blocks = new List<Block>();
        for (int i = 0; i < solids.Count; i++)
        {
            Box b = solids[i];
            blocks.Add(NewBlock(PieceNames[i], b));
        }

        var marker = new System.Numerics.Vector2(exit.GlobalPosition.X, exit.GlobalPosition.Y);
        Node room = exit.Owner ?? exit;
        List<Block> inPlane = room.Descendants<Block>()
            .Where(b => !b.CollisionOnly && b.GetParent() is not SeamEnd && Mathf.Abs(b.GlobalPosition.Z) < b.Size.Z * 0.5f)
            .ToList();
        List<Block> walls = inPlane.Where(b => !b.Cracked).ToList();
        List<Box> wallBoxes = walls.Select(b => b.PlayBox).ToList();
        (Material look, bool lined) = Look(frame.Solids(exit.Side, marker), walls, wallBoxes, blocks[0]);
        (bool head, bool foot) = Walled;
        BuildWall(frame, exit, marker, room, inPlane.Select(b => b.PlayBox), blocks, head, foot);
        foreach (Block block in blocks)
        {
            block.Material = look;
            AddChild(block);
        }

        Hang(frame, exit, marker, look);
        bool hangs = !foot && frame.SillHangs(exit.Side, marker, wallBoxes);
        BuildTrim(frame.Trim(exit.Side, lined, hangs), look, blocks[0].MetalMaterial);

        if (Flooded)
        {
            Box passage = frame.Passage(exit.Side, new System.Numerics.Vector2(exit.GlobalPosition.X, exit.GlobalPosition.Y));
            WaterBody water = (exit.Owner ?? throw new InvalidOperationException($"{exit.NameText()} is in no room")).Descendants<WaterBody>()
                .FirstOrDefault(w => w.Swim && w.PlayRegion.Overlaps(passage))
                ?? throw new InvalidOperationException($"{GetType().Name} on {exit.NameText()} is flooded, but no swum water of its room reaches the opening");
            water.Flood(passage);
        }
    }

    /// <summary>
    /// What hangs from this kind of end into its room past the frame (a <see cref="Hatch"/>'s standpipe), built once the frame is,
    /// before its trim (which keeps off any water surface) and its flooding (which asks the room's water to fill the passage).
    /// <paramref name="look"/>: the frame's look.
    /// </summary>
    protected virtual void Hang(SeamFrame frame, RoomExit exit, System.Numerics.Vector2 marker, Material look)
    {
    }

    /// <summary>Whether a doorway's frame is walled over its lintel and under its sill (<see cref="SeamFrame.Wall"/>); a hatch's never is.</summary>
    protected virtual (bool Head, bool Foot) Walled => (false, false);

    /// <summary>
    /// The wall the frame stands in (<see cref="SeamFrame.Wall"/>), as more of the frame's blocks: stopped by the room's own blocks in
    /// the play plane, its other doorways' frames and the walls those already built, else by the camera bounds, and left open under or over its hatches. A wall column as wide as the lintel or the sill joins it
    /// into one block, so no joint line crosses the wall at the frame's edge.
    /// </summary>
    private void BuildWall(SeamFrame frame, RoomExit exit, System.Numerics.Vector2 marker, Node room, IEnumerable<Box> solids, List<Block> blocks, bool head, bool foot)
    {
        if (!head && !foot)
        {
            return;
        }

        List<RoomExit> others = room.Descendants<RoomExit>().Where(e => e != exit).ToList();
        var built = room.Descendants<Block>().Where(b => b.GetParent() is SeamEnd end && end != this).Select(b => b.PlayBox); // other ends' walls, once built
        var stops = solids.Concat(others.SelectMany(e => OtherFrame(e, hole: false))).Concat(built).ToList();
        var holes = others.Where(e => e.Threshold.IsHatch).SelectMany(e => OtherFrame(e, hole: true)).ToList();
        Vector3 origin = room is Node3D r ? r.GlobalPosition : Vector3.Zero;
        Box bounds = room is Room bounded
            ? new Box(origin.X + bounded.BoundsMin.X, origin.Y + bounded.BoundsMin.Y, origin.X + bounded.BoundsMax.X, origin.Y + bounded.BoundsMax.Y)
            : new Box(marker.X, marker.Y, marker.X, marker.Y);
        int extra = 0;
        foreach (Box w in frame.Wall(exit.Side, marker, stops, bounds, head, foot, holes))
        {
            Box local = new(w.MinX - marker.X, w.MinY - marker.Y, w.MaxX - marker.X, w.MaxY - marker.Y);
            Block below = w.MinY >= marker.Y ? blocks[1] : blocks[0];
            Box piece = Box.Around(below.Position.Flat(), new System.Numerics.Vector2(below.Size.X, below.Size.Y));
            if (MathF.Abs(local.MinX - piece.MinX) < 0.01f && MathF.Abs(local.MaxX - piece.MaxX) < 0.01f)
            {
                Box joined = new(piece.MinX, MathF.Min(piece.MinY, local.MinY), piece.MaxX, MathF.Max(piece.MaxY, local.MaxY));
                Resize(below, joined);
                continue;
            }

            blocks.Add(NewBlock($"{(w.MinY >= marker.Y ? "Head" : "Foot")}{++extra}", local));
        }
    }

    /// <summary>
    /// The frame and opening of another seam end of the room, whose blocks may not be built yet: a wall stops there. With
    /// <paramref name="hole"/>, a hatch's hole alone, through its slab.
    /// </summary>
    private static Box[] OtherFrame(RoomExit other, bool hole)
    {
        SeamEnd? end = other.Children().OfType<SeamEnd>().FirstOrDefault();
        if (end?.Profile is null)
        {
            return Array.Empty<Box>();
        }

        Box area = end.Profile.Frame.Exit(other.Side, new System.Numerics.Vector2(other.GlobalPosition.X, other.GlobalPosition.Y)).Area;
        return new[] { hole ? area with { MinY = area.MinY - SeamCheck.Around, MaxY = area.MaxY + SeamCheck.Around } : area.Grown(SeamCheck.Around) };
    }

    /// <summary>
    /// What the frame looks like: <see cref="Material"/> when set; else the look of the room's own block it meets the most
    /// (<see cref="SeamFrame.Neighbour"/>), so the frame is of the wall around it in any biome, unless that look would lie about the
    /// frame's layer. A metal frame set in rock takes the rock's look and is lined with metal where the climb runs (<c>lined</c>).
    /// </summary>
    private (Material Look, bool Lined) Look(IReadOnlyList<Box> frame, List<Block> walls, List<Box> wallBoxes, Block made)
    {
        Material own = Material ?? (Surface == SurfaceKind.Metal ? made.MetalMaterial : made.RockMaterial);
        if (Material is not null)
        {
            return (own, false);
        }

        int? meets = SeamFrame.Neighbour(frame, wallBoxes);
        if (meets is not { } index)
        {
            return (own, false);
        }

        Block wall = walls[index];
        Material wallLook = wall.Material ?? (wall.Surface == SurfaceKind.Metal ? wall.MetalMaterial : wall.RockMaterial);
        return (Surface, wall.Surface) switch
        {
            (SurfaceKind.Metal, SurfaceKind.Rock) => (wallLook, true),
            _ when Surface == wall.Surface => (wallLook, false),
            _ => (own, false),
        };
    }

    /// <summary>The frame's trim (<see cref="SeamFrame.Trim"/>) as bevelled kit pieces on its front face: no collision.</summary>
    private void BuildTrim(IReadOnlyList<TrimPiece> pieces, Material look, Material metal)
    {
        float front = Profile.Depth * 0.5f, overlap = TrimOverlap;
        List<WaterBody> waters = Owner is null ? new() : Owner.Descendants<WaterBody>().ToList();
        for (int i = 0; i < pieces.Count; i++)
        {
            TrimPiece p = ClearOfWater(pieces[i], waters);
            float z0 = p.Through ? -front - overlap : front - p.Sink, z1 = p.Through ? front + overlap : front + p.Proud;
            var size = new Vector3(p.Face.MaxX - p.Face.MinX, p.Face.MaxY - p.Face.MinY, z1 - z0);
            AddChild(new MeshInstance3D
            {
                Mesh = new BlockMesh { Size = size, Bevel = MathF.Min(TrimBevel, MathF.Min(size.X, size.Y) * TrimBevelShare) },
                MaterialOverride = p.Lining ? metal : look,
                Position = new Vector3((p.Face.MinX + p.Face.MaxX) * 0.5f, (p.Face.MinY + p.Face.MaxY) * 0.5f, (z0 + z1) * 0.5f),
            }.Named($"Trim{i}"));
        }
    }

    /// <summary>The piece lowered off a water surface its top would lie on (a face on the surface would fight it).</summary>
    private TrimPiece ClearOfWater(TrimPiece piece, List<WaterBody> waters)
    {
        Vector3 at = GlobalPosition;
        float top = at.Y + piece.Face.MaxY;
        bool onSurface = waters.Any(w => Mathf.Abs(top - w.SurfaceY) < WaterTouch && w.PlayRegion.MinX < at.X + piece.Face.MaxX && at.X + piece.Face.MinX < w.PlayRegion.MaxX);
        return onSurface ? piece with { Face = piece.Face with { MaxY = piece.Face.MaxY - WaterLower } } : piece;
    }

    /// <summary>A frame block named <paramref name="name"/> filling <paramref name="local"/> (this end's meters), of the frame's depth and surface.</summary>
    protected Block NewBlock(string name, Box local)
    {
        var block = BlockScene.Instantiate<Block>();
        block.Named(name);
        block.Surface = Surface;
        Resize(block, local);
        return block;
    }

    /// <summary>Sets <paramref name="block"/> to fill <paramref name="local"/> (this end's meters), at the frame's depth.</summary>
    private void Resize(Block block, Box local)
    {
        block.Size = new Vector3(local.MaxX - local.MinX, local.MaxY - local.MinY, Profile.Depth);
        block.Position = new Vector3((local.MinX + local.MaxX) * 0.5f, (local.MinY + local.MaxY) * 0.5f, 0f);
    }
}
