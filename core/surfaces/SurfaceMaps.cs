namespace TheDeep;

/// <summary>
/// One baked texture set (<see cref="SurfaceBaker"/>): two square RGB8 images of <see cref="Size"/> texels a side, rows from the
/// top of the tile down, that tile in both directions. Written as <c>&lt;set&gt;_normal.png</c> and <c>&lt;set&gt;_surface.png</c> in
/// <c>assets/textures/surfaces/</c> (<c>SurfaceBake</c>).
/// <list type="bullet">
/// <item><see cref="Normal"/>: the tangent-space normal, OpenGL style as Godot reads it (red along the face, green up it, blue out
/// of it). Imported as a normal map, so a material reads it with <c>hint_normal</c> and takes only red and green (Godot rebuilds
/// blue).</item>
/// <item><see cref="Surface"/>: data, read linear (no <c>source_color</c>). Red is the albedo tone divided by
/// <see cref="ToneScale"/>, green the cavity, blue the dry roughness (<see cref="SurfacePoint"/>).</item>
/// </list>
/// A material samples both at <c>uv = vec2(along, -up) / tile</c>, with <c>along</c> and <c>up</c> the world position on the face's
/// tangent and bitangent (meters, so the pattern runs on across neighboring pieces) and <c>tile</c> the set's
/// <see cref="ISurfacePattern.TileMeters"/>, through a sampler with <c>repeat_enable</c> and
/// <c>filter_linear_mipmap_anisotropic</c>: the mipmaps are what keep the fine detail still (<c>docs/art-direction.md#stable-image</c>).
/// </summary>
public sealed class SurfaceMaps
{
    /// <summary>The surface map's red channel holds the tone divided by this, so a tone up to twice the palette color fits.</summary>
    public const double ToneScale = 2.0;

    public required int Size { get; init; }

    /// <summary>RGB8, <see cref="Size"/> squared texels.</summary>
    public required byte[] Normal { get; init; }

    /// <summary>RGB8, <see cref="Size"/> squared texels.</summary>
    public required byte[] Surface { get; init; }
}
