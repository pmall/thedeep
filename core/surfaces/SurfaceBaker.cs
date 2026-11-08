using System;
using System.Threading.Tasks;

namespace TheDeep;

/// <summary>
/// Bakes a surface pattern into its texture set (<see cref="SurfaceMaps"/>): the pattern sampled at every texel's center, its
/// height turned into normals by central differences that wrap round the tile's edges, so the set tiles without a seam. The same
/// pattern always bakes to the same bytes (rows are computed in parallel, each texel on its own). Pure logic, unit tested.
/// </summary>
public static class SurfaceBaker
{
    public static SurfaceMaps Bake(ISurfacePattern pattern, int size)
    {
        double tile = pattern.TileMeters;
        double texel = tile / size;
        var points = new SurfacePoint[size * size];
        Parallel.For(0, size, row =>
        {
            double up = (size - row - 0.5) * texel;
            for (int column = 0; column < size; column++)
            {
                points[(row * size) + column] = pattern.At((column + 0.5) * texel, up);
            }
        });

        byte[] normal = new byte[size * size * 3];
        byte[] surface = new byte[size * size * 3];
        for (int row = 0; row < size; row++)
        {
            int above = TileNoise.Wrap(row - 1, size);
            int below = TileNoise.Wrap(row + 1, size);
            for (int column = 0; column < size; column++)
            {
                int left = TileNoise.Wrap(column - 1, size);
                int right = TileNoise.Wrap(column + 1, size);
                double slopeAlong = (points[(row * size) + right].HeightMeters - points[(row * size) + left].HeightMeters) / (2.0 * texel);
                double slopeUp = (points[(above * size) + column].HeightMeters - points[(below * size) + column].HeightMeters) / (2.0 * texel);
                double length = Math.Sqrt((slopeAlong * slopeAlong) + (slopeUp * slopeUp) + 1.0);

                int i = ((row * size) + column) * 3;
                normal[i] = Unit((-slopeAlong / length * 0.5) + 0.5);
                normal[i + 1] = Unit((-slopeUp / length * 0.5) + 0.5);
                normal[i + 2] = Unit((1.0 / length * 0.5) + 0.5);

                SurfacePoint p = points[(row * size) + column];
                surface[i] = Unit(p.Tone / SurfaceMaps.ToneScale);
                surface[i + 1] = Unit(p.Cavity);
                surface[i + 2] = Unit(p.Roughness);
            }
        }

        return new SurfaceMaps { Size = size, Normal = normal, Surface = surface };
    }

    /// <summary>A value in [0, 1] as a byte.</summary>
    private static byte Unit(double value) => (byte)Math.Round(Math.Clamp(value, 0.0, 1.0) * 255.0, MidpointRounding.AwayFromZero);
}
