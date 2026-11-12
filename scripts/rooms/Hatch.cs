using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// A hatch's frame (a <see cref="SeamEnd"/>): on a <see cref="RoomExit"/> in a floor or ceiling, the slab either side of the hole, as
/// thick as the exit's area, so the room's own floor or ceiling stops where the frame starts. What lies past the slab is each room's
/// own: each bounds its camera at the slab's far face, so the seam check sees only the frame. A flooded hatch in a roof may hang a
/// <see cref="Standpipe"/> down into the room's lower water.
/// </summary>
public partial class Hatch : SeamEnd
{
    /// <summary>
    /// Whether this flooded hatch in a roof holds its water down to the room's own (<see cref="SeamFrame.Pipe"/>): a column of water
    /// from the slab into the highest swum <see cref="WaterBody"/> under the hole, walled by two <see cref="Block"/>s whose mouth stops
    /// a body's height over that water, so a floater swims in under it and floats up through the hatch. Needs <see cref="SeamEnd.Flooded"/>;
    /// the room check fails when no swum water lies under the whole hole.
    /// </summary>
    [Export] public bool Standpipe { get; set; }

    /// <summary>Optional look of the standpipe's walls; without it, the frame's.</summary>
    [Export] public Material? PipeMaterial { get; set; }

    /// <summary>The look of the standpipe's front (<c>standpipe_section.tres</c>): a thicker veil than a pool's, so the column reads as water.</summary>
    [Export] public Material PipeSection { get; set; } = null!;

    protected override bool FramesHatch => true;

    protected override string[] PieceNames { get; } = { "SlabWest", "SlabEast" };

    protected override void Hang(SeamFrame frame, RoomExit exit, System.Numerics.Vector2 marker, Material look)
    {
        if (!Standpipe)
        {
            return;
        }

        if (!Flooded)
        {
            throw new InvalidOperationException($"Hatch on {exit.Name} hangs a standpipe but isn't flooded: the pipe holds the way's water");
        }

        Node room = exit.Owner ?? throw new InvalidOperationException($"{exit.Name} is in no room");
        List<WaterBody> pools = Waters(room).Where(w => w.Swim).ToList();
        var pipe = frame.Pipe(exit.Side, marker, pools.Select(w => w.PlayRegion).ToList())
            ?? throw new InvalidOperationException($"Hatch on {exit.Name} hangs a standpipe, but no swum water of its room lies under the whole hole");
        WaterBody pool = pools[pipe.Pool];
        Vector3 at = GlobalPosition;
        Box c = pipe.Column;
        AddChild(new WaterBody
        {
            Name = "Standpipe",
            Size = new Vector2(c.MaxX - c.MinX, Profile.Depth + 0.1f),
            Depth = c.MaxY - c.MinY,
            Murk = pool.Murk,
            Reflection = pool.Reflection,
            Swim = true,
            Globals = false,
            SurfaceMaterial = pool.SurfaceMaterial,
            SectionMaterial = PipeSection,
            Position = new Vector3((c.MinX + c.MaxX) * 0.5f - at.X, c.MaxY - at.Y, -at.Z),
        });

        foreach ((string name, Box w) in new[] { ("PipeWest", pipe.West), ("PipeEast", pipe.East) })
        {
            var wall = BlockScene.Instantiate<Block>();
            wall.Name = name;
            wall.Size = new Vector3(w.MaxX - w.MinX, w.MaxY - w.MinY, Profile.Depth);
            wall.Position = new Vector3((w.MinX + w.MaxX) * 0.5f - at.X, (w.MinY + w.MaxY) * 0.5f - at.Y, 0f);
            wall.Surface = Surface;
            wall.Material = PipeMaterial ?? look;
            AddChild(wall);
        }
    }
}
