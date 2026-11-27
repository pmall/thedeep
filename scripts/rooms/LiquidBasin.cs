using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Visual-only containment behind play-plane liquid troughs (<see cref="SlagBody"/> and narrow swum <see cref="WaterBody"/> pools),
/// so a channel cut into the play plane never ends in open air behind <see cref="PlayPlane.Back"/> (docs/roadmap.md#graphic-flaws).
/// Inherits the look and lip height of the flanking play-plane blocks (as <see cref="SeamEnd"/> does), adds no collision, and leaves
/// gameplay bounds and <see cref="GroundRules"/> untouched.
/// </summary>
internal static class LiquidBasin
{
    /// <summary>Thickness of the visual back wall behind <see cref="PlayPlane.Back"/>, meters.</summary>
    public const float BackThickness = 1.6f;

    /// <summary>How far the back wall tucks past each side bank behind <see cref="PlayPlane.Back"/> so corners never crack open, meters.</summary>
    private const float SideTuck = 0.2f;

    /// <summary>Tolerance for a flanking block to count as touching the liquid's side or bed, meters.</summary>
    private const float Touch = 0.25f;

    /// <summary>Widest swum pool treated as a contained play-plane trough rather than an open flooded chamber, meters.</summary>
    private const float MaxContainedWidth = 16f;

    /// <summary>Lowest, default and highest height of the back lip above the liquid's surface, meters.</summary>
    private const float MinLip = 0.35f;
    private const float DefaultLip = 0.55f;
    private const float MaxLip = 0.75f;

    /// <summary>Bevel on the back wall's edges, matching <see cref="Block.Bevel"/>, meters.</summary>
    private const float Bevel = 0.05f;

    /// <summary>
    /// Whether <paramref name="water"/> is a contained play-plane pool (narrow, swum, flanked by play-plane blocks, with no second
    /// water body continuing behind <see cref="PlayPlane.Back"/> at its level) rather than an open flooded hall or lake.
    /// </summary>
    public static bool IsContainedPool(WaterBody water, Room? room)
    {
        if (!water.Swim || room is null || water.GetParent() is SeamEnd || water.Size.X > MaxContainedWidth)
        {
            return false;
        }

        Box region = water.PlayRegion;
        bool openBehind = room.Descendants<WaterBody>().Any(other =>
            other != water
            && Mathf.Abs(other.SurfaceY - water.SurfaceY) < 0.5f
            && other.PlayRegion.MinX < region.MaxX
            && other.PlayRegion.MaxX > region.MinX
            && other.Area.Position.Y < PlayPlane.Back - 0.5f
            && other.Area.End.Y >= PlayPlane.Back - 0.5f);
        if (openBehind)
        {
            return false;
        }

        return PlayPlaneBlocks(room).Any(b => IsFlanking(b.PlayBox, region));
    }

    /// <summary>
    /// Finds the material and lip height (<paramref name="bankTop"/>) of the play-plane rock/masonry flanking
    /// <paramref name="playRegion"/> in <paramref name="room"/>.
    /// </summary>
    public static Material? FindLook(Room? room, Box playRegion, out float bankTop)
    {
        float surface = playRegion.MaxY;
        bankTop = surface + DefaultLip;
        if (room is null)
        {
            return null;
        }

        List<Block> blocks = PlayPlaneBlocks(room);
        var flanking = blocks.Where(b => IsFlanking(b.PlayBox, playRegion)).ToList();
        var rising = flanking.Where(b => b.PlayBox.MaxY > surface + 0.05f).ToList();
        if (rising.Count > 0)
        {
            float lowestBank = rising.Min(b => b.PlayBox.MaxY);
            bankTop = Mathf.Clamp(lowestBank, surface + MinLip, surface + MaxLip);
        }

        Block? sample = flanking.FirstOrDefault()
            ?? blocks.FirstOrDefault(b => IsBed(b.PlayBox, playRegion))
            ?? blocks.FirstOrDefault();
        if (sample is null)
        {
            return null;
        }

        return sample.Material ?? (sample.Surface == SurfaceKind.Metal ? sample.MetalMaterial : sample.RockMaterial);
    }

    /// <summary>
    /// Builds a visual-only back containment wall immediately behind <paramref name="backZ"/> (world Z), spanning
    /// <paramref name="playRegion"/> horizontally (tucked behind its banks) and from <paramref name="playRegion"/>'s bottom up to
    /// <paramref name="backTop"/>, positioned in the owning <paramref name="origin"/>'s local space.
    /// </summary>
    public static MeshInstance3D BuildBackWall(Vector3 origin, Box playRegion, float backZ, float backTop, Material? look)
    {
        float width = (playRegion.MaxX - playRegion.MinX) + 2f * SideTuck;
        float height = MathF.Max(0.25f, backTop - playRegion.MinY);
        float centerX = 0.5f * (playRegion.MinX + playRegion.MaxX) - origin.X;
        float centerY = 0.5f * (playRegion.MinY + backTop) - origin.Y;
        float centerZ = backZ - 0.5f * BackThickness - origin.Z;
        return new MeshInstance3D
        {
            Mesh = new BlockMesh { Size = new Vector3(width, height, BackThickness), Bevel = Bevel },
            MaterialOverride = look,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(centerX, centerY, centerZ),
        }.Named("BasinBack");
    }

    private static List<Block> PlayPlaneBlocks(Room room) =>
        room.Descendants<Block>()
            .Where(b => !b.CollisionOnly && !b.Cracked && b.GetParent() is not SeamEnd && Mathf.Abs(b.GlobalPosition.Z) < b.Size.Z * 0.5f)
            .ToList();

    private static bool IsFlanking(Box block, Box liquid)
    {
        bool overlapsY = block.MaxY > liquid.MinY + 0.05f && block.MinY < liquid.MaxY + 1.5f;
        bool touchesLeft = MathF.Abs(block.MaxX - liquid.MinX) <= Touch;
        bool touchesRight = MathF.Abs(block.MinX - liquid.MaxX) <= Touch;
        return overlapsY && (touchesLeft || touchesRight);
    }

    private static bool IsBed(Box block, Box liquid) =>
        MathF.Abs(block.MaxY - liquid.MinY) <= Touch && block.MaxX > liquid.MinX && block.MinX < liquid.MaxX;
}
