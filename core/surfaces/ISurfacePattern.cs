namespace TheDeep;

/// <summary>
/// A procedural surface pattern baked into a tileable texture set (<see cref="SurfaceBaker"/>): a square tile of the face plane,
/// <see cref="TileMeters"/> on a side, that repeats in both directions without a seam.
/// </summary>
public interface ISurfacePattern
{
    /// <summary>The tile's side, meters.</summary>
    double TileMeters { get; }

    /// <summary>
    /// The surface at a point of the tile: <paramref name="alongMeters"/> runs along the face (the face tangent), and
    /// <paramref name="upMeters"/> up it (the face bitangent), both from the tile's corner. The pattern repeats every
    /// <see cref="TileMeters"/> along both.
    /// </summary>
    SurfacePoint At(double alongMeters, double upMeters);
}
