using System;

namespace TheDeep;

/// <summary>
/// Whether an image pops or blinks (docs/testing.md#settle-reel, docs/art-direction.md#stable-image). Frames are compared
/// pixel by pixel, in luma (0–255; <see cref="Luma"/> turns an RGBA8 frame into a luma plane).
///
/// A pop: with only the camera moving, by <c>step</c> screen pixels at the play plane, the image of a continuous scene
/// changes at each pixel by at most its local contrast (the largest difference to a neighbor, in either frame) times the
/// step. A pixel that changes by more than <see cref="Slack"/> times that, plus <see cref="Tolerance"/>, pops: something
/// in it is not a continuous function of the camera position (a geometry edge with no anti-aliasing, a line thinner than
/// a pixel, a threshold that flips, a pick among neighbors). With the camera still, any change over the tolerance pops.
///
/// A blink: with the world running and the camera still, a pixel that jumps by more than <see cref="BlinkThreshold"/> one
/// frame and back by as much the next (grain, a glint that flashes on one pixel). Pure logic, unit tested.
/// </summary>
public static class FrameShimmer
{
    /// <summary>
    /// A luma change (0–255) no frame-to-frame comparison counts: under what the eye can tell in one frame, and the 8-bit
    /// rounding of a value that moves by a fraction of a level.
    /// </summary>
    public const int Tolerance = 4;

    /// <summary>
    /// How far a pixel may change past its contrast times the camera step: room for the layers nearer than the play plane,
    /// which move faster than it, and for a contrast measured between neighbors rather than at the pixel.
    /// </summary>
    public const double Slack = 2.0;

    /// <summary>A jump (0–255) that comes straight back the next frame, with the camera still, above which a pixel blinks.</summary>
    public const int BlinkThreshold = 12;

    /// <summary>How far the camera is stepped each frame of a sweep, in screen pixels at the play plane.</summary>
    public const double SweepStep = 1.0 / 16.0;

    /// <summary>A camera that moves by less than this (screen pixels) in a frame stands still.</summary>
    public const double StillShift = 0.001;

    /// <summary>A camera that moves by more than <see cref="StillShift"/> and less than this (screen pixels) in a frame creeps.</summary>
    public const double CreepShift = 1.0;

    /// <summary>Whether a camera that moved <paramref name="shiftPixels"/> in a frame creeps.</summary>
    public static bool Creeps(double shiftPixels) => shiftPixels is >= StillShift and < CreepShift;

    /// <summary>What changed between two frames: the mean luma difference (0–255) and how many pixels popped (or blinked).</summary>
    public readonly record struct Change(double MeanDifference, int Pops);

    /// <summary>The comparisons of one part of a run.</summary>
    public sealed class Phase
    {
        public int Frames { get; private set; }

        /// <summary>The mean luma difference between consecutive frames.</summary>
        public double MeanDifference { get; private set; }

        /// <summary>Pops (or blinks) over every frame of the phase.</summary>
        public long Pops { get; private set; }

        /// <summary>The most pops in one frame.</summary>
        public int WorstFrame { get; private set; }

        public void Add(Change change)
        {
            Frames++;
            MeanDifference += (change.MeanDifference - MeanDifference) / Frames;
            Pops += change.Pops;
            WorstFrame = Math.Max(WorstFrame, change.Pops);
        }
    }

    /// <summary>Writes the luma of an RGBA8 frame (alpha ignored) into <paramref name="luma"/>, one byte per pixel.</summary>
    public static void Luma(ReadOnlySpan<byte> rgba, Span<byte> luma)
    {
        if (rgba.Length < luma.Length * 4)
        {
            throw new ArgumentException($"frame shorter than {luma.Length} RGBA pixels");
        }

        for (int i = 0; i < luma.Length; i++)
        {
            int o = i * 4;
            luma[i] = (byte)((54 * rgba[o] + 183 * rgba[o + 1] + 19 * rgba[o + 2]) >> 8);
        }
    }

    /// <summary>
    /// Compares two luma frames of <paramref name="width"/> × <paramref name="height"/> taken with the camera moved by
    /// <paramref name="step"/> screen pixels between them (0: still). When <paramref name="heat"/> is not empty (one byte
    /// per pixel), each popping pixel keeps there the largest change it has shown.
    /// </summary>
    public static Change Compare(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after, int width, int height, double step, Span<byte> heat)
    {
        int pixels = width * height;
        if (before.Length < pixels || after.Length < pixels)
        {
            throw new ArgumentException($"frames shorter than {width}x{height}");
        }

        if (!heat.IsEmpty && heat.Length < pixels)
        {
            throw new ArgumentException($"heat shorter than {pixels} pixels");
        }

        double reach = Slack * Math.Max(step, 0.0);
        long sum = 0;
        int pops = 0;
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = row + x;
                int d = Math.Abs(before[i] - after[i]);
                sum += d;
                if (d <= Tolerance)
                {
                    continue;
                }

                double allowed = Tolerance + reach * Math.Max(Contrast(before, width, height, x, y), Contrast(after, width, height, x, y));
                if (d <= allowed)
                {
                    continue;
                }

                pops++;
                if (!heat.IsEmpty && d > heat[i])
                {
                    heat[i] = (byte)d;
                }
            }
        }

        return pixels == 0 ? default : new Change((double)sum / pixels, pops);
    }

    /// <summary>
    /// Counts the pixels of <paramref name="middle"/> that jump by more than <see cref="BlinkThreshold"/> from
    /// <paramref name="first"/> and back by as much in <paramref name="last"/> (luma frames of <paramref name="pixels"/>);
    /// each keeps the smaller of its two jumps in <paramref name="heat"/> when it is not empty.
    /// </summary>
    public static Change Blinks(ReadOnlySpan<byte> first, ReadOnlySpan<byte> middle, ReadOnlySpan<byte> last, int pixels, Span<byte> heat)
    {
        if (first.Length < pixels || middle.Length < pixels || last.Length < pixels)
        {
            throw new ArgumentException($"frames shorter than {pixels} pixels");
        }

        long sum = 0;
        int blinks = 0;
        for (int i = 0; i < pixels; i++)
        {
            int up = middle[i] - first[i];
            int down = last[i] - middle[i];
            sum += Math.Abs(down);
            int jump = Math.Min(Math.Abs(up), Math.Abs(down));
            if ((up ^ down) >= 0 || jump <= BlinkThreshold)
            {
                continue;
            }

            blinks++;
            if (!heat.IsEmpty && jump > heat[i])
            {
                heat[i] = (byte)jump;
            }
        }

        return pixels == 0 ? default : new Change((double)sum / pixels, blinks);
    }

    /// <summary>The largest luma difference between a pixel and its four neighbors.</summary>
    private static int Contrast(ReadOnlySpan<byte> luma, int width, int height, int x, int y)
    {
        int i = y * width + x;
        int c = luma[i];
        int most = 0;
        if (x > 0)
        {
            most = Math.Max(most, Math.Abs(c - luma[i - 1]));
        }

        if (x + 1 < width)
        {
            most = Math.Max(most, Math.Abs(c - luma[i + 1]));
        }

        if (y > 0)
        {
            most = Math.Max(most, Math.Abs(c - luma[i - width]));
        }

        if (y + 1 < height)
        {
            most = Math.Max(most, Math.Abs(c - luma[i + width]));
        }

        return most;
    }
}
