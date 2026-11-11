using System;

namespace TheDeep;

/// <summary>
/// The relief of a metal panel, as a tileable pattern (<see cref="ISurfacePattern"/>) for the metal material
/// (<c>assets/shaders/metal.gdshader</c>): the narrow seam between two panels, the raised bevel along it, and what a panel carries
/// near its corners. The tile holds four square panels of <see cref="PanelMeters"/>, one of each <see cref="PanelVariant"/>, the
/// variant <c>i</c> in the quadrant <c>(i % 2, i / 2)</c> (along, up). Seams run along the tile's edges and its middle lines, so
/// every panel's seam is shared with its neighbor and identical all round.
/// <para>
/// The material fits panels of any size to each face, so it never shows the tile as it is: it folds each of its panels onto one
/// of these, sampling the panel's corner region in each corner and its middle lines all along a longer edge. That holds because
/// every panel is symmetric about its middle lines and plain within <see cref="FlatMeters"/> of them: everything but the seam's
/// profile lies within <see cref="ReachMeters"/> of a seam. Its <see cref="SurfacePoint.Tone"/> multiplies the coating's color, its
/// cavity is the seam and the dirt traps around what stands proud, its roughness is the coating's dry roughness about
/// <see cref="Roughness"/>, and its cell is 1 on a rivet's head and 0 elsewhere (the material reads it as a coverage, so a mip
/// that blends a head with the panel gives a share of it). The knobs are its properties (meters unless named otherwise).
/// </para>
/// </summary>
public sealed record PanelSurface : ISurfacePattern
{
    /// <summary>What a panel carries near its corners, in the order of the tile's quadrants.</summary>
    public enum PanelVariant
    {
        /// <summary>Seam and bevel only.</summary>
        Plain,

        /// <summary>A rivet in each corner.</summary>
        Riveted,

        /// <summary>A small plate across each corner, three rivets through it.</summary>
        Gusset,

        /// <summary>The middle of the panel sunk a little behind a chamfered frame.</summary>
        Inset,
    }

    public double TileMeters => 2.0 * PanelMeters;

    /// <summary>A panel's side: two to a tile side.</summary>
    public double PanelMeters { get; init; } = 0.5;

    /// <summary>How far from a seam a panel's features reach; beyond it, up to its middle lines, a panel is flat.</summary>
    public double ReachMeters => (PanelMeters / 2.0) - FlatMeters;

    /// <summary>The flat band either side of a panel's middle lines.</summary>
    public double FlatMeters { get; init; } = 0.05;

    /// <summary>The seam's half width, its depth, and how dark its bottom is.</summary>
    public double SeamMeters { get; init; } = 0.014;

    /// <summary>How deep the seam's bottom lies below the panels' edges, meters.</summary>
    public double SeamDepthMeters { get; init; } = 0.008;

    /// <summary>What the coating's tone is multiplied by at the seam's bottom (below 1 darkens).</summary>
    public double SeamTone { get; init; } = 0.25;

    /// <summary>The bevel beside the seam: how wide, and how high the panel's face stands once over it.</summary>
    public double BevelMeters { get; init; } = 0.05;

    /// <summary>How high a panel's face stands above its edge once over the bevel, meters.</summary>
    public double BevelRiseMeters { get; init; } = 0.012;

    /// <summary>A rivet: its distance from the seams, its radius, its head's height and tone, the dirt ring round it.</summary>
    public double RivetInsetMeters { get; init; } = 0.1;

    /// <summary>A corner rivet's radius, meters.</summary>
    public double RivetMeters { get; init; } = 0.028;

    /// <summary>How high a rivet's head stands at its crown, meters.</summary>
    public double RivetHeightMeters { get; init; } = 0.006;

    /// <summary>The tone of a rivet's head (1 is the coating's).</summary>
    public double RivetTone { get; init; } = 1.08;

    /// <summary>How far the dirt ring reaches beyond a rivet's rim, meters.</summary>
    public double RingMeters { get; init; } = 0.025;

    /// <summary>The corner plate: its inner edge's distance from the seams, how far it reaches, its thickness and tone; its
    /// rivets' distances from the seams and radius.</summary>
    public double GussetFromMeters { get; init; } = 0.035;

    /// <summary>How far the corner plate reaches from the seams along them, meters; its outer corner is cut on the
    /// diagonal.</summary>
    public double GussetToMeters { get; init; } = 0.185;

    /// <summary>The corner plate's thickness, meters.</summary>
    public double GussetMeters { get; init; } = 0.003;

    /// <summary>The corner plate's tone (1 is the coating's).</summary>
    public double GussetTone { get; init; } = 1.04;

    /// <summary>The distance from both seams of the plate's corner rivet, and from the nearer seam of its two others,
    /// meters.</summary>
    public double GussetRivetNearMeters { get; init; } = 0.075;

    /// <summary>The distance from the farther seam of the plate's two outer rivets, meters.</summary>
    public double GussetRivetFarMeters { get; init; } = 0.14;

    /// <summary>The radius of a corner plate's rivet, meters.</summary>
    public double GussetRivetMeters { get; init; } = 0.02;

    /// <summary>The sunk middle: its frame's width from the seams, its corners' radius, its depth and the chamfer down to it.</summary>
    public double InsetMeters { get; init; } = 0.13;

    /// <summary>The radius of the sunk middle's rounded corners, meters.</summary>
    public double InsetCornerMeters { get; init; } = 0.04;

    /// <summary>How far the middle is sunk behind its frame, meters.</summary>
    public double InsetDepthMeters { get; init; } = 0.004;

    /// <summary>The width of the chamfer from the frame down to the sunk middle, meters.</summary>
    public double InsetChamferMeters { get; init; } = 0.012;

    /// <summary>The coating's roughness, and the rivet heads' (matte, so their small domes never glint).</summary>
    public double Roughness { get; init; } = 0.32;

    /// <summary>The roughness (0 to 1) at the seam's bottom.</summary>
    public double SeamRoughness { get; init; } = 0.5;

    /// <summary>The roughness (0 to 1) of a rivet's head.</summary>
    public double RivetRoughness { get; init; } = 0.48;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double side = PanelMeters;
        double along = TileNoise.Wrap(alongMeters, TileMeters);
        double up = TileNoise.Wrap(upMeters, TileMeters);
        int qx = along < side ? 0 : 1;
        int qy = up < side ? 0 : 1;
        var variant = (PanelVariant)(qx + (2 * qy));

        // Distances to the panel's seams, folded onto its corner: the panel is symmetric about its middle lines.
        double x = along - (qx * side);
        double y = up - (qy * side);
        x = Math.Min(x, side - x);
        y = Math.Min(y, side - y);
        double toSeam = Math.Min(x, y);

        double seam = 1.0 - TileNoise.SmoothStep(SeamMeters - 0.003, SeamMeters + 0.003, toSeam);
        double rise = TileNoise.SmoothStep(SeamMeters, SeamMeters + BevelMeters, toSeam);
        double height = (rise * BevelRiseMeters) - (seam * SeamDepthMeters);
        double tone = 1.0;
        double cavity = seam;
        double rough = TileNoise.Lerp(Roughness, SeamRoughness, seam);
        double head = 0.0;

        switch (variant)
        {
            case PanelVariant.Riveted:
                Rivet(x, y, RivetInsetMeters, RivetInsetMeters, RivetMeters, ref height, ref cavity, ref head);
                break;
            case PanelVariant.Gusset:
            {
                // A plate over the corner, its far corner cut off square across the diagonal.
                double inner = Math.Min(x, y) - GussetFromMeters;
                double outer = Math.Min(GussetToMeters - Math.Max(x, y), (GussetToMeters * 1.6) - (x + y));
                double plate = TileNoise.SmoothStep(0.0, 0.004, Math.Min(inner, outer));
                height += plate * GussetMeters;
                tone = TileNoise.Lerp(tone, GussetTone, plate);
                cavity = Math.Max(cavity, 0.3 * TileNoise.Line(Math.Min(inner, outer), 0.008) * (1.0 - plate));
                Rivet(x, y, GussetRivetNearMeters, GussetRivetNearMeters, GussetRivetMeters, ref height, ref cavity, ref head);
                Rivet(x, y, GussetRivetNearMeters, GussetRivetFarMeters, GussetRivetMeters, ref height, ref cavity, ref head);
                Rivet(x, y, GussetRivetFarMeters, GussetRivetNearMeters, GussetRivetMeters, ref height, ref cavity, ref head);
                break;
            }

            case PanelVariant.Inset:
            {
                // How far inside the sunk middle: a rounded rectangle InsetMeters from the seams.
                double corner = InsetMeters + InsetCornerMeters;
                double inside = x < corner && y < corner
                    ? InsetCornerMeters - Math.Sqrt(((corner - x) * (corner - x)) + ((corner - y) * (corner - y)))
                    : Math.Min(x, y) - InsetMeters;
                double sunk = TileNoise.SmoothStep(0.0, InsetChamferMeters, inside);
                height -= sunk * InsetDepthMeters;
                cavity = Math.Max(cavity, 0.35 * TileNoise.Line(inside - InsetChamferMeters, 0.01));
                break;
            }
        }

        tone = TileNoise.Lerp(tone, RivetTone, head);
        tone *= 1.0 - (seam * (1.0 - SeamTone));
        rough = TileNoise.Lerp(rough, RivetRoughness, head);
        return new SurfacePoint(height, tone, cavity, rough, head > 0.5 ? 1 : 0);
    }

    /// <summary>A rivet at (<paramref name="cx"/>, <paramref name="cy"/>) from the seams: a low head, nearly flat on top with a
    /// rounded shoulder (a full dome would be a point of light that glints), and a ring of dirt round its foot.</summary>
    private void Rivet(double x, double y, double cx, double cy, double radius, ref double height, ref double cavity, ref double head)
    {
        double d = Math.Sqrt(((x - cx) * (x - cx)) + ((y - cy) * (y - cy)));
        double t = d / radius;
        double on = 1.0 - TileNoise.SmoothStep(0.94, 1.0, t);
        double crown = (1.0 - TileNoise.SmoothStep(0.55, 1.0, t)) * (1.0 - (0.25 * t * t));
        height += RivetHeightMeters * crown;
        cavity = Math.Max(cavity, 0.35 * TileNoise.Line(d - radius, RingMeters) * (1.0 - on));
        head = Math.Max(head, on);
    }
}
