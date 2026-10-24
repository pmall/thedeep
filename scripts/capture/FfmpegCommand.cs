using System.Collections.Generic;
using System.Globalization;

namespace TheDeep;

/// <summary>H.264 encoders the video capture can use, best first.</summary>
public enum VideoCodec
{
    /// <summary>NVIDIA hardware encoder: nearly free for the CPU.</summary>
    Nvenc,

    /// <summary>Software fallback, works everywhere ffmpeg does.</summary>
    X264,
}

/// <summary>
/// The ffmpeg command line that turns raw frames on stdin into an MP4. Pure logic, unit tested.
/// The MP4 is fragmented, so the file stays playable even if the game crashes mid-recording.
/// </summary>
/// <param name="Width">Width of the raw input frames, in pixels.</param>
/// <param name="Height">Height of the raw input frames, in pixels.</param>
/// <param name="PixelFormat">ffmpeg name of the raw input layout, like "rgba".</param>
/// <param name="Fps">Video frame rate.</param>
/// <param name="Codec">Encoder to use.</param>
/// <param name="MaxWidth">Wider input is scaled down to this width (aspect kept).</param>
/// <param name="OutputPath">The MP4 file to write.</param>
public sealed record FfmpegCommand(
    int Width, int Height, string PixelFormat, int Fps, VideoCodec Codec, int MaxWidth, string OutputPath)
{
    public const string Executable = "ffmpeg";

    public IReadOnlyList<string> Arguments()
    {
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-nostats", "-y",
            "-f", "rawvideo",
            "-pix_fmt", PixelFormat,
            "-s", $"{Width}x{Height}",
            "-framerate", Fps.ToString(CultureInfo.InvariantCulture),
            "-i", "-",
            // Cap the width and round both sides to even numbers (yuv420p needs them).
            "-vf", $"scale=trunc(min(iw\\,{MaxWidth})/2)*2:-2",
        };
        args.AddRange(CodecArguments(Codec));
        args.AddRange(new[]
        {
            "-g", (Fps * 2).ToString(CultureInfo.InvariantCulture),
            "-pix_fmt", "yuv420p",
            "-movflags", "+frag_keyframe+empty_moov+default_base_moof",
            OutputPath,
        });
        return args;
    }

    /// <summary>Encodes one tiny frame to nowhere: exits with 0 only if <paramref name="codec"/> works here.</summary>
    public static IReadOnlyList<string> ProbeArguments(VideoCodec codec)
    {
        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-f", "lavfi", "-i", "color=c=black:s=256x256",
            "-frames:v", "1",
        };
        args.AddRange(CodecArguments(codec));
        args.AddRange(new[] { "-f", "null", "-" });
        return args;
    }

    public static string CodecName(VideoCodec codec) => codec switch
    {
        VideoCodec.Nvenc => "h264_nvenc",
        _ => "libx264",
    };

    private static string[] CodecArguments(VideoCodec codec) => codec switch
    {
        VideoCodec.Nvenc => new[] { "-c:v", CodecName(codec), "-preset", "p5", "-tune", "hq", "-rc", "vbr", "-cq", "20", "-b:v", "0" },
        _ => new[] { "-c:v", CodecName(codec), "-preset", "veryfast", "-crf", "20" },
    };
}
