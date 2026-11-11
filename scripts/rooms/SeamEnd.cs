using System;
using Godot;

namespace TheDeep;

/// <summary>
/// One room's end of a seam, framed from its <see cref="Profile"/> (<see cref="SeamFrame"/> has the rule): placed on a
/// <see cref="RoomExit"/> (as its child, where it stays), it sizes the exit and builds the frame around the opening as
/// <see cref="Block"/>s. The room at the other end frames its exit with the same profile, so the seam agrees by construction
/// (docs/gameplay.md#seam-rule): neither room places a block of its own there, and its own solids stop where the frame starts. The
/// frame's blocks look like any block of the room (its biome's material for their <see cref="Surface"/>). A
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
        for (int i = 0; i < solids.Count; i++)
        {
            Box b = solids[i];
            var block = BlockScene.Instantiate<Block>();
            block.Name = PieceNames[i];
            block.Size = new Vector3(b.MaxX - b.MinX, b.MaxY - b.MinY, Profile.Depth);
            block.Position = new Vector3((b.MinX + b.MaxX) * 0.5f, (b.MinY + b.MaxY) * 0.5f, 0f);
            block.Surface = Surface;
            block.Material = Material;
            AddChild(block);
        }
    }
}
