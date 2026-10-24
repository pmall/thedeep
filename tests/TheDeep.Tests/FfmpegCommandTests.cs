using System.Linq;
using Xunit;

namespace TheDeep.Tests;

public class FfmpegCommandTests
{
    private static readonly FfmpegCommand Command = new(1600, 900, "rgba", 60, VideoCodec.Nvenc, 1920, "/tmp/out.mp4");

    [Fact]
    public void Reads_raw_frames_of_the_given_size_from_stdin()
    {
        string line = string.Join(' ', Command.Arguments());
        Assert.Contains("-f rawvideo -pix_fmt rgba -s 1600x900 -framerate 60 -i -", line);
    }

    [Fact]
    public void Writes_a_crash_safe_mp4_last()
    {
        var args = Command.Arguments();
        Assert.Equal("/tmp/out.mp4", args[^1]);
        Assert.Contains("+frag_keyframe+empty_moov+default_base_moof", args);
    }

    [Fact]
    public void Caps_width_and_keeps_sides_even()
    {
        var args = Command.Arguments().ToList();
        Assert.Equal("scale=trunc(min(iw\\,1920)/2)*2:-2", args[args.IndexOf("-vf") + 1]);
    }

    [Theory]
    [InlineData(VideoCodec.Nvenc, "h264_nvenc")]
    [InlineData(VideoCodec.X264, "libx264")]
    public void Uses_the_chosen_codec(VideoCodec codec, string name)
    {
        var args = (Command with { Codec = codec }).Arguments().ToList();
        Assert.Equal(name, args[args.IndexOf("-c:v") + 1]);
        Assert.Contains(name, FfmpegCommand.ProbeArguments(codec));
    }
}
