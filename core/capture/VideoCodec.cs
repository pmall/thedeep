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
