using System;
using Godot;

namespace TheDeep;

/// <summary>
/// A doorway's frame: placed on a <see cref="RoomExit"/> in a west or east wall (as its child, where it stays), it builds the sill under
/// the opening and the lintel over it as <see cref="Block"/>s, and sizes the exit, all from its <see cref="Profile"/>
/// (<see cref="SeamFrame"/> has the rule). The room at the other end frames its exit with the same profile, so the seam agrees by
/// construction (docs/gameplay.md#seam-rule): neither room places a block of its own there, and its walls and floor stop where the
/// frame starts. The frame's blocks look like any block of the room (its biome's material for their <see cref="Surface"/>).
/// </summary>
public partial class Doorway : Node3D
{
    /// <summary>The kind of seam, the same at both ends of the way.</summary>
    [Export] public SeamProfile Profile { get; set; } = null!;

    /// <summary>What the frame is made of (its physics layer and default material, as a <see cref="Block"/>'s).</summary>
    [Export] public SurfaceKind Surface { get; set; } = SurfaceKind.Rock;

    /// <summary>Optional look replacing the default material of <see cref="Surface"/>.</summary>
    [Export] public Material? Material { get; set; }

    /// <summary>The block the frame is built of (<c>scenes/kit/Block.tscn</c>).</summary>
    [Export] public PackedScene BlockScene { get; set; } = null!;

    public override void _Ready()
    {
        var exit = GetParent() as RoomExit ?? throw new InvalidOperationException($"Doorway {Name} must be the child of a RoomExit");
        if (exit.Threshold.IsHatch)
        {
            throw new InvalidOperationException($"Doorway {Name} is on {exit.Name}, a hatch: a doorway frames a west or east exit");
        }

        SeamFrame frame = Profile.Frame;
        exit.Size = frame.ExitSize(exit.Side).ToGodot();
        Transform = Transform3D.Identity; // centered on its exit by construction, whatever the scene says

        string[] names = { "Sill", "Lintel" };
        var solids = frame.Solids(exit.Side, System.Numerics.Vector2.Zero);
        for (int i = 0; i < solids.Count; i++)
        {
            Box b = solids[i];
            var block = BlockScene.Instantiate<Block>();
            block.Name = names[i];
            block.Size = new Vector3(b.MaxX - b.MinX, b.MaxY - b.MinY, Profile.Depth);
            block.Position = new Vector3((b.MinX + b.MaxX) * 0.5f, (b.MinY + b.MaxY) * 0.5f, 0f);
            block.Surface = Surface;
            block.Material = Material;
            AddChild(block);
        }
    }
}
