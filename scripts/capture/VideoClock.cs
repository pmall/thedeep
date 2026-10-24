using System;

namespace TheDeep;

/// <summary>
/// Turns wall-clock recording time into a constant-rate video: each rendered game frame asks how
/// many video frames are due, so the video plays at real speed whatever the game's frame rate
/// (frames are repeated when the game renders slower than the video rate, skipped when faster).
/// Pure logic, unit tested.
/// </summary>
public sealed class VideoClock
{
    private readonly int _maxBurst;

    /// <param name="fps">Video frame rate.</param>
    /// <param name="maxBurst">Most frames one call may return; longer stalls are cut from the video.</param>
    public VideoClock(int fps, int maxBurst)
    {
        if (fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));
        if (maxBurst <= 0) throw new ArgumentOutOfRangeException(nameof(maxBurst));
        Fps = fps;
        _maxBurst = maxBurst;
    }

    public int Fps { get; }

    /// <summary>Video frames handed out so far (including repeats).</summary>
    public long FramesEmitted { get; private set; }

    /// <summary>Video frames lost to stalls longer than the burst limit.</summary>
    public long FramesCut { get; private set; }

    public double DurationSeconds => (double)FramesEmitted / Fps;

    /// <summary>
    /// How many video frames the current game frame should fill, <paramref name="elapsedSeconds"/>
    /// after recording started. The first call (at 0 s) returns 1.
    /// </summary>
    public int Advance(double elapsedSeconds)
    {
        long target = (long)Math.Floor(Math.Max(0.0, elapsedSeconds) * Fps) + 1;
        long due = target - FramesEmitted - FramesCut;
        if (due <= 0)
        {
            return 0;
        }

        if (due > _maxBurst)
        {
            FramesCut += due - _maxBurst;
            due = _maxBurst;
        }

        FramesEmitted += due;
        return (int)due;
    }
}
