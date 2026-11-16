using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The numbers of a room's atmosphere stack (<c>RoomStage</c>): where the skylines stand, the dark framing silhouettes
/// above and below the play area, the dust and the salts of the stage's seeds. Meters unless said.
/// </summary>
public sealed record StageSpec
{
    /// <summary>The approved look.</summary>
    public static readonly StageSpec Default = new();

    /// <summary>The widescreen aspect (width over height) the backdrop is sized for.</summary>
    public float WideAspect { get; init; } = 16f / 9f;

    /// <summary>The backdrop's view width is this much more than the exact view's, so its ends never show.</summary>
    public float ViewMargin { get; init; } = 1.15f;

    /// <summary>The far skyline's foot is this far under the room's floor...</summary>
    public float FarSkylineBelow { get; init; } = 6f;

    /// <summary>...and its sheet at least this high.</summary>
    public float FarSkylineMinHeight { get; init; } = 90f;

    /// <summary>The nearer skyline's foot is this far under the room's floor...</summary>
    public float NearSkylineBelow { get; init; } = 4f;

    /// <summary>...and its sheet at least this high.</summary>
    public float NearSkylineMinHeight { get; init; } = 60f;

    /// <summary>A skyline sheet is the room's height plus this share of its least height.</summary>
    public float SkylineHeightShare { get; init; } = 0.6f;

    /// <summary>The framing silhouettes reach this far past the room's sides.</summary>
    public float FrameSideMargin { get; init; } = 4f;

    /// <summary>The silhouettes below the room: one to every this many meters of width (the room's and its side margins).</summary>
    public float FrameBelowSpacing { get; init; } = 9.5f;

    /// <summary>The silhouettes above the room: one to every this many meters of width.</summary>
    public float FrameAboveSpacing { get; init; } = 15f;

    /// <summary>The bases of the silhouettes below stand between this far under the room's floor...</summary>
    public float FrameBelowFrom { get; init; } = 4f;

    /// <summary>...and this far.</summary>
    public float FrameBelowTo { get; init; } = 3f;

    /// <summary>The tops of the silhouettes above stand between this far over the room's ceiling...</summary>
    public float FrameAboveFrom { get; init; } = 2f;

    /// <summary>...and this far.</summary>
    public float FrameAboveTo { get; init; } = 3f;

    /// <summary>The silhouettes stand in front of the play plane, between this many meters of it...</summary>
    public float FrameDepthMin { get; init; } = 6f;

    /// <summary>...and this many.</summary>
    public float FrameDepthMax { get; init; } = 9f;

    /// <summary>The least size of a silhouette below.</summary>
    public Vector3 FrameBelowSizeMin { get; init; } = new(0.6f, 3f, 0.6f);

    /// <summary>The greatest size of a silhouette below.</summary>
    public Vector3 FrameBelowSizeMax { get; init; } = new(1.8f, 6.5f, 1.5f);

    /// <summary>The least size of a silhouette above.</summary>
    public Vector3 FrameAboveSizeMin { get; init; } = new(0.4f, 3f, 0.4f);

    /// <summary>The greatest size of a silhouette above.</summary>
    public Vector3 FrameAboveSizeMax { get; init; } = new(1f, 6.5f, 1f);

    /// <summary>A stage's seed is its room's times this, plus a salt...</summary>
    public int SeedStride { get; init; } = 7;

    /// <summary>...of this for the silhouettes below...</summary>
    public int FrameBelowSalt { get; init; } = 37;

    /// <summary>...this for the ones above...</summary>
    public int FrameAboveSalt { get; init; } = 41;

    /// <summary>...and this for the architecture.</summary>
    public int ArchitectureSalt { get; init; } = 11;

    /// <summary>
    /// Where the underglow shines, as a direction: up, a little sideways, and into the scene (onto undersides and the faces of the
    /// walls behind).
    /// </summary>
    public Vector3 UnderglowDirection { get; init; } = new(0.25f, 0.8f, -0.55f);

    /// <summary>The dust's middle is this far under the room's middle, and this far behind the play plane.</summary>
    public Vector2 DustSink { get; init; } = new(1f, 5.5f);

    /// <summary>The dust fills this share of the room's height (half of it each way)...</summary>
    public float DustHeightShare { get; init; } = 0.4f;

    /// <summary>...and this half-depth.</summary>
    public float DustHalfDepth { get; init; } = 6.5f;

    /// <summary>The seed of a stage's piece: the room's seed times <see cref="SeedStride"/>, plus <paramref name="salt"/>.</summary>
    public int Seed(int roomSeed, int salt) => roomSeed * SeedStride + salt;

    /// <summary>
    /// How many silhouettes frame the room's bottom (or top, with <paramref name="above"/>) and the area their bases stand
    /// in, for a room spanning <paramref name="min"/> to <paramref name="max"/>.
    /// </summary>
    public FrameBand Frame(Vector2 min, Vector2 max, bool above)
    {
        float width = max.X - min.X + 2f * FrameSideMargin;
        float spacing = above ? FrameAboveSpacing : FrameBelowSpacing;
        int count = Math.Max(1, (int)MathF.Round(width / spacing));
        return above
            ? new FrameBand(count, new Vector2(min.X - FrameSideMargin, max.Y + FrameAboveFrom), new Vector2(max.X + FrameSideMargin, max.Y + FrameAboveTo))
            : new FrameBand(count, new Vector2(min.X - FrameSideMargin, min.Y - FrameBelowFrom), new Vector2(max.X + FrameSideMargin, min.Y - FrameBelowTo));
    }
}
