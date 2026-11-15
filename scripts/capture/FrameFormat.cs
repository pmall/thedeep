namespace TheDeep;

/// <summary>Size and byte layout of a grabbed frame. <see cref="PixelFormat"/> is the ffmpeg name.</summary>
public readonly record struct FrameFormat(int Width, int Height, string PixelFormat)
{
    /// <summary>Bytes in one frame: four per pixel.</summary>
    public int ByteCount => Width * Height * 4;
}
