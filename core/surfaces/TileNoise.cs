using System;

namespace TheDeep;

/// <summary>
/// Noise that tiles, for the patterns baked into surface textures (<see cref="SurfaceBaker"/>): value noise on an integer lattice
/// that wraps, so a pattern built from it repeats exactly every period and its texture has no seam. Coordinates are in lattice
/// cells and a period is a whole number of cells. Hashing is on integers only, so the same arguments give the same value on every
/// machine and every run. Pure logic, unit tested.
/// </summary>
public static class TileNoise
{
    /// <summary>A hash of a lattice point, in [0, 1).</summary>
    public static double Hash(int x, int y, int seed)
    {
        // Each coordinate scattered by its own odd constant, then mixed by the "lowbias32" finalizer.
        uint h = unchecked(((uint)x * 0x8DA6B343u) ^ ((uint)y * 0xD8163841u) ^ ((uint)seed * 0xCB1AB31Fu));
        h ^= h >> 16;
        h = unchecked(h * 0x7FEB352Du);
        h ^= h >> 15;
        h = unchecked(h * 0x846CA68Bu);
        h ^= h >> 16;
        return h / 4294967296.0;
    }

    /// <summary><paramref name="i"/> brought into [0, <paramref name="period"/>).</summary>
    public static int Wrap(int i, int period) => ((i % period) + period) % period;

    /// <summary><paramref name="x"/> brought into [0, <paramref name="period"/>).</summary>
    public static double Wrap(double x, double period) => x - (period * Math.Floor(x / period));

    /// <summary>Value noise in [0, 1], smooth (quintic), repeating every <paramref name="periodX"/> by <paramref name="periodY"/> cells.</summary>
    public static double Value(double x, double y, int periodX, int periodY, int seed)
    {
        double fx = Math.Floor(x);
        double fy = Math.Floor(y);
        int ix = (int)fx;
        int iy = (int)fy;
        double ux = Quintic(x - fx);
        double uy = Quintic(y - fy);
        int x0 = Wrap(ix, periodX);
        int x1 = Wrap(ix + 1, periodX);
        int y0 = Wrap(iy, periodY);
        int y1 = Wrap(iy + 1, periodY);
        double bottom = Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), ux);
        double top = Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), ux);
        return Lerp(bottom, top, uy);
    }

    /// <summary>
    /// A fractal sum of <paramref name="octaves"/> octaves of <see cref="Value"/>, in [0, 1]: each octave twice the frequency and
    /// half the amplitude of the one before, its period doubled with it, so the sum repeats with the first octave's period.
    /// </summary>
    public static double Fractal(double x, double y, int periodX, int periodY, int octaves, int seed)
    {
        double sum = 0.0;
        double total = 0.0;
        double amplitude = 0.5;
        int scale = 1;
        for (int octave = 0; octave < octaves; octave++)
        {
            sum += amplitude * Value(x * scale, y * scale, periodX * scale, periodY * scale, seed + (octave * 1013));
            total += amplitude;
            amplitude *= 0.5;
            scale *= 2;
        }

        return sum / total;
    }

    /// <summary>1 at the center of a line and 0 from <paramref name="width"/> away, rounded at both ends (a groove's profile).</summary>
    public static double Line(double distance, double width) => 1.0 - SmoothStep(0.0, width, Math.Abs(distance));

    public static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - (2.0 * t));
    }

    private static double Quintic(double t) => t * t * t * ((t * ((t * 6.0) - 15.0)) + 10.0);

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
