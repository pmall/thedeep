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
/// frame's blocks take the look of the room's wall they meet, and its trim (molding proud of the front face, visual only) makes the
/// frame read as a portal built on purpose wherever it stands. A
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

    /// <summary>The block the frame is built of (<c>scenes/kit/Block.tscn</c>).</summary>
    [Export] public PackedScene BlockScene { get; set; } = null!;

    /// <summary>Whether this kind of end frames an exit through a slab (a hatch) rather than a wall (a doorway).</summary>
    protected abstract bool FramesHatch { get; }

    /// <summary>The names of the frame's blocks, in the order <see cref="SeamFrame.Solids"/> gives them.</summary>
    protected abstract string[] PieceNames { get; }

    public override void _Ready()
    {
        var exit = GetParent() as RoomExit ?? throw new InvalidOperationException($"{GetType().Name} {Name} must be the child of a RoomExit");
        if (exit.Threshold.IsHatch != FramesHatch)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} {Name} is on {exit.Name}, a {(exit.Threshold.IsHatch ? "hatch" : "doorway")}: it frames the other kind");
        }

        SeamFrame frame = Profile.Frame;
        exit.Size = frame.ExitSize(exit.Side).ToGodot();
        Transform = Transform3D.Identity; // centered on its exit by construction, whatever the scene says

        var solids = frame.Solids(exit.Side, System.Numerics.Vector2.Zero);
        var blocks = new List<Block>();
        for (int i = 0; i < solids.Count; i++)
        {
            Box b = solids[i];
            var block = BlockScene.Instantiate<Block>();
            block.Name = PieceNames[i];
            block.Size = new Vector3(b.MaxX - b.MinX, b.MaxY - b.MinY, Profile.Depth);
            block.Position = new Vector3((b.MinX + b.MaxX) * 0.5f, (b.MinY + b.MaxY) * 0.5f, 0f);
            block.Surface = Surface;
            blocks.Add(block);
        }

        var marker = new System.Numerics.Vector2(exit.GlobalPosition.X, exit.GlobalPosition.Y);
        List<Block> walls = Blocks(exit.Owner ?? exit)
            .Where(b => !b.Cracked && !b.CollisionOnly && b.GetParent() is not SeamEnd && Mathf.Abs(b.GlobalPosition.Z) < b.Size.Z * 0.5f)
            .ToList();
        List<Box> wallBoxes = walls.Select(PlayBox).ToList();
        (Material look, bool lined) = Look(frame.Solids(exit.Side, marker), walls, wallBoxes, blocks[0]);
        foreach (Block block in blocks)
        {
            block.Material = look;
            AddChild(block);
        }

        BuildTrim(frame.Trim(exit.Side, lined, frame.SillHangs(exit.Side, marker, wallBoxes)), look, blocks[0].MetalMaterial);

        if (Flooded)
        {
            Box passage = frame.Passage(exit.Side, new System.Numerics.Vector2(exit.GlobalPosition.X, exit.GlobalPosition.Y));
            WaterBody water = Waters(exit.Owner ?? throw new InvalidOperationException($"{exit.Name} is in no room"))
                .FirstOrDefault(w => w.Swim && w.PlayRegion.Overlaps(passage))
                ?? throw new InvalidOperationException($"{GetType().Name} on {exit.Name} is flooded, but no swum water of its room reaches the opening");
            water.Flood(passage);
        }
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
        float front = Profile.Depth * 0.5f, overlap = 0.04f;
        List<WaterBody> waters = Owner is null ? new() : Waters(Owner).ToList();
        for (int i = 0; i < pieces.Count; i++)
        {
            TrimPiece p = ClearOfWater(pieces[i], waters);
            float z0 = p.Through ? -front - overlap : front - p.Sink, z1 = p.Through ? front + overlap : front + p.Proud;
            var size = new Vector3(p.Face.MaxX - p.Face.MinX, p.Face.MaxY - p.Face.MinY, z1 - z0);
            AddChild(new MeshInstance3D
            {
                Name = $"Trim{i}",
                Mesh = new BlockMesh { Size = size, Bevel = MathF.Min(0.04f, MathF.Min(size.X, size.Y) * 0.25f) },
                MaterialOverride = p.Lining ? metal : look,
                Position = new Vector3((p.Face.MinX + p.Face.MaxX) * 0.5f, (p.Face.MinY + p.Face.MaxY) * 0.5f, (z0 + z1) * 0.5f),
            });
        }
    }

    /// <summary>The piece lowered off a water surface its top would lie on (a face on the surface would fight it).</summary>
    private TrimPiece ClearOfWater(TrimPiece piece, List<WaterBody> waters)
    {
        Vector3 at = GlobalPosition;
        float top = at.Y + piece.Face.MaxY;
        bool onSurface = waters.Any(w => Mathf.Abs(top - w.SurfaceY) < 0.1f && w.PlayRegion.MinX < at.X + piece.Face.MaxX && at.X + piece.Face.MinX < w.PlayRegion.MaxX);
        return onSurface ? piece with { Face = piece.Face with { MaxY = piece.Face.MaxY - 0.15f } } : piece;
    }

    /// <summary>A block's box on the play plane, in world meters.</summary>
    private static Box PlayBox(Block b)
    {
        Vector3 c = b.GlobalPosition, s = b.Size;
        return new Box(c.X - s.X * 0.5f, c.Y - s.Y * 0.5f, c.X + s.X * 0.5f, c.Y + s.Y * 0.5f);
    }

    /// <summary>The blocks under <paramref name="node"/>, at any depth.</summary>
    private static IEnumerable<Block> Blocks(Node node) =>
        node.GetChildren().SelectMany(child => child is Block block ? new[] { block } : Blocks(child));

    /// <summary>The water bodies under <paramref name="node"/>, at any depth.</summary>
    private static IEnumerable<WaterBody> Waters(Node node) =>
        node.GetChildren().SelectMany(child => child is WaterBody water ? new[] { water } : Waters(child));
}
