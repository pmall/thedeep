using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace TheDeep.Tests;

/// <summary>Runs the real ffmpeg, a required tool for the F10 video capture (docs/toolchain.md).</summary>
public class VideoEncoderTests
{
    private const int Width = 64;
    private const int Height = 48;

    [Fact]
    public void Finds_a_working_codec()
    {
        Assert.NotNull(VideoEncoder.DetectCodec());
    }

    [Fact]
    public async Task Encodes_frames_into_a_playable_file()
    {
        string path = Path.Combine(Path.GetTempPath(), $"thedeep-test-{Guid.NewGuid():N}.mp4");
        try
        {
            var pool = new CountingPool();
            VideoEncoder encoder = VideoEncoder.Start(Command(path), pool);
            for (int i = 0; i < 10; i++)
            {
                encoder.Submit(Frame((byte)(i * 20)), 2);
                Thread.Sleep(5); // let the writer keep up: a full queue would turn frames into repeats
            }

            var (exitCode, errors) = await encoder.FinishAsync(TimeSpan.FromSeconds(20));

            Assert.True(exitCode == 0, errors);
            Assert.Equal(20, encoder.FramesWritten);
            Assert.True(new FileInfo(path).Length > 0);
            Assert.Equal(10, pool.Returned);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Missed_frames_are_filled_by_repeats()
    {
        string path = Path.Combine(Path.GetTempPath(), $"thedeep-test-{Guid.NewGuid():N}.mp4");
        try
        {
            VideoEncoder encoder = VideoEncoder.Start(Command(path));
            encoder.CoverMissedFrames(3);
            encoder.Submit(Frame(0), 1);
            var (exitCode, errors) = await encoder.FinishAsync(TimeSpan.FromSeconds(20));

            Assert.True(exitCode == 0, errors);
            Assert.Equal(4, encoder.FramesWritten);
            Assert.Equal(3, encoder.FramesRepeatedForLag);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Frames_after_finishing_are_ignored_and_given_back()
    {
        string path = Path.Combine(Path.GetTempPath(), $"thedeep-test-{Guid.NewGuid():N}.mp4");
        try
        {
            var pool = new CountingPool();
            VideoEncoder encoder = VideoEncoder.Start(Command(path), pool);
            encoder.Submit(Frame(0), 1);
            await encoder.FinishAsync(TimeSpan.FromSeconds(20));
            encoder.Submit(Frame(0), 1);

            Assert.Equal(1, encoder.FramesWritten);
            Assert.Equal(2, pool.Returned);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static FfmpegCommand Command(string path) => new(Width, Height, "rgba", 30, VideoCodec.X264, 1920, path);

    private static byte[] Frame(byte shade)
    {
        var frame = new byte[Width * Height * 4];
        Array.Fill(frame, shade);
        return frame;
    }

    private sealed class CountingPool : ArrayPool<byte>
    {
        private int _returned;

        public int Returned => _returned;

        public override byte[] Rent(int minimumLength) => new byte[minimumLength];

        public override void Return(byte[] array, bool clearArray = false) => Interlocked.Increment(ref _returned);
    }
}
