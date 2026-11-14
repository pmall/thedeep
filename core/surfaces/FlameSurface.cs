namespace TheDeep;

/// <summary>
/// The flames of a furnace mouth's fire (<c>assets/shaders/furnace_mouth.gdshader</c>), as a tileable pattern
/// (<see cref="ISurfacePattern"/>): a soft fractal noise, nothing else. Only its <see cref="SurfacePoint.Tone"/> matters, which
/// the surface texture's red channel carries as the noise in [0, 1] (tone over <see cref="SurfaceMaps.ToneScale"/>); the shader
/// scrolls it up and thresholds it into tongues of flame. Baked, so nothing generates a noise texture at run time.
/// </summary>
public sealed record FlameSurface : ISurfacePattern
{
    public double TileMeters { get; init; } = 1.0;

    /// <summary>The noise's first octave's cells per tile.</summary>
    public int Cells { get; init; } = 6;

    /// <summary>The seed of the noise.</summary>
    public int Seed { get; init; } = 11;

    public SurfacePoint At(double alongMeters, double upMeters)
    {
        double u = TileNoise.Wrap(alongMeters, TileMeters) / TileMeters;
        double v = TileNoise.Wrap(upMeters, TileMeters) / TileMeters;
        double noise = TileNoise.Fractal(u * Cells, v * Cells, Cells, Cells, 3, Seed);
        return new SurfacePoint(0.0, noise * SurfaceMaps.ToneScale, 0.0, 1.0);
    }
}
