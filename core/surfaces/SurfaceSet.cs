using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// A texture set the materials sample (<c>docs/art-direction.md#stable-image</c>): a pattern baked at <paramref name="Size"/>
/// texels a side into <c>assets/textures/surfaces/&lt;Name&gt;_normal.png</c> and <c>&lt;Name&gt;_surface.png</c> (the layout and how
/// to sample them: <see cref="SurfaceMaps"/>). <see cref="All"/> is every set the bake writes.
/// </summary>
public sealed record SurfaceSet(string Name, ISurfacePattern Pattern, int Size)
{
    /// <summary>Every baked set. About 256 texels a meter (the metal's panels 512, as fine as their seams need, and the machines'
    /// glyph bands 800, as fine as their strokes): some 2.8 a pixel at the gameplay camera, so close-ups stay sharp.</summary>
    public static IReadOnlyList<SurfaceSet> All { get; } = new[]
    {
        new SurfaceSet("strata", new StrataSurface(), 1024),
        new SurfaceSet("basalt", new BasaltSurface(), 1024),
        new SurfaceSet("masonry", new MasonrySurface(), 1024),
        new SurfaceSet("ice", new IceSurface(), 1024),
        new SurfaceSet("cracks", new CracksSurface(), 1024),
        new SurfaceSet("panels", new PanelSurface(), 512),
        new SurfaceSet("coating", new CoatingSurface(), 1024),
        new SurfaceSet("glyphs", new GlyphBandSurface(), 512),
        new SurfaceSet("frost", new FrostSurface(), 1024),
        new SurfaceSet("flames", new FlameSurface(), 256),
    };
}
