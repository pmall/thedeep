using System;

namespace TheDeep;

/// <summary>
/// How much an image crawls under a camera that creeps (docs/testing.md#settle-reel). Consecutive frames are compared
/// pixel by pixel (their luma). With the world frozen and the camera stepped by <see cref="SweepStep"/> of a pixel a
/// frame, a well-filtered image changes each pixel by at most that fraction of its local contrast, under
/// <see cref="FlickerThreshold"/>; a pixel that changes more pops: detail that is not filtered (a line thinner than a pixel,
/// a threshold that flips, a normal from coarse derivatives) and changes as much for a sixteenth of a pixel as for a whole
/// one, which the eye sees as shimmer. With the camera still, nothing should change at all. Pure logic, unit tested.
/// </summary>
public static class FrameShimmer
{
    /// <summary>A luma change (0–255) above which a pixel pops.</summary>
    public const int FlickerThreshold = 16;

    /// <summary>How far the camera is stepped each frame of a sweep, in screen pixels at the play plane.</summary>
    public const double SweepStep = 1.0 / 16.0;

    /// <summary>A camera that moves by less than this (screen pixels) in a frame stands still.</summary>
    public const double StillShift = 0.001;

    /// <summary>A camera that moves by more than <see cref="StillShift"/> and less than this (screen pixels) in a frame creeps.</summary>
    public const double CreepShift = 1.0;

    /// <summary>Whether a camera that moved <paramref name="shiftPixels"/> in a frame creeps.</summary>
    public static bool Creeps(double shiftPixels) => shiftPixels is >= StillShift and < CreepShift;

    /// <summary>What changed between two frames: the mean luma difference (0–255) and the share of popping pixels (0–1).</summary>
    public readonly record struct Change(double MeanDifference, double FlickerShare);

    /// <summary>The comparisons of one part of a run, averaged.</summary>
    public sealed class Phase
    {
        public int Frames { get; private set; }

        /// <summary>The mean luma difference between consecutive frames.</summary>
        public double MeanDifference { get; private set; }

        /// <summary>The share of popping pixels.</summary>
        public double FlickerShare { get; private set; }

        public void Add(Change change)
        {
            Frames++;
            MeanDifference += (change.MeanDifference - MeanDifference) / Frames;
            FlickerShare += (change.FlickerShare - FlickerShare) / Frames;
        }
    }

    /// <summary>
    /// Compares two RGBA8 frames of <paramref name="pixels"/> pixels each (alpha ignored). When <paramref name="heat"/>
    /// is not empty (one byte per pixel), each of its pixels keeps the largest luma change it has seen.
    /// </summary>
    public static Change Compare(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after, int pixels, Span<byte> heat)
    {
        if (before.Length < pixels * 4 || after.Length < pixels * 4)
        {
            throw new ArgumentException($"frames shorter than {pixels} RGBA pixels");
        }

        if (!heat.IsEmpty && heat.Length < pixels)
        {
            throw new ArgumentException($"heat shorter than {pixels} pixels");
        }

        long sum = 0;
        int flickering = 0;
        for (int i = 0; i < pixels; i++)
        {
            int o = i * 4;
            int a = Luma(before[o], before[o + 1], before[o + 2]);
            int b = Luma(after[o], after[o + 1], after[o + 2]);
            int d = Math.Abs(a - b);
            sum += d;
            if (d > FlickerThreshold)
            {
                flickering++;
            }

            if (!heat.IsEmpty && d > heat[i])
            {
                heat[i] = (byte)d;
            }
        }

        return pixels == 0 ? default : new Change((double)sum / pixels, (double)flickering / pixels);
    }

    /// <summary>Rec. 709 luma of an 8-bit color, 0–255.</summary>
    private static int Luma(byte r, byte g, byte b) => (54 * r + 183 * g + 19 * b) >> 8;
}
