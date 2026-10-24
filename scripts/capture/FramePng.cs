using System;
using Godot;

namespace TheDeep;

/// <summary>Saves a grabbed frame (see <see cref="FrameGrabber"/>) as a PNG. Safe to call from a worker thread.</summary>
public static class FramePng
{
    /// <summary>Writes <paramref name="bytes"/> to <paramref name="path"/> and returns the array to <see cref="FrameGrabber.Buffers"/>.</summary>
    public static Error Save(byte[] bytes, FrameFormat format, string path)
    {
        Image image = Image.CreateFromData(format.Width, format.Height, false, Image.Format.Rgba8, bytes.AsSpan(0, format.ByteCount));
        FrameGrabber.Buffers.Return(bytes);
        image.Convert(Image.Format.Rgb8); // the alpha channel of a frame is meaningless
        return image.SavePng(path);
    }
}
