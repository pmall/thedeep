using System;
using System.Buffers;
using System.Collections.Generic;
using Godot;
using Godot.NativeInterop;

namespace TheDeep;

/// <summary>
/// Copies a viewport's finished image back from the GPU without stalling the frame: the copy is
/// queued with the frame's rendering and the bytes arrive a frame or two later in a callback.
/// A request made during a frame gets the previous frame, before anything drawn in this one.
///
/// Frames are copied into arrays rented from <see cref="Buffers"/> rather than marshalled into
/// new ones: at 60 frames per second, fresh multi-megabyte arrays would make the .NET garbage
/// collector run full collections several times a second. The readback's callable and the frame's format are made once (the format
/// again when the window's size changes), so a request allocates nothing.
/// </summary>
public sealed class FrameGrabber
{
    /// <summary>Where frame arrays come from. Whoever receives a frame returns it here when done.</summary>
    public static readonly ArrayPool<byte> Buffers = ArrayPool<byte>.Create(maxArrayLength: 64 * 1024 * 1024, maxArraysPerBucket: 16);

    private readonly Viewport _viewport;
    private readonly RenderingDevice _device;

    // Readbacks arrive in the order they were asked for; each waits here with what to call and the number to call it with.
    private readonly Queue<(Action<byte[], int> OnFrame, int Argument)> _waiting = new();
    private readonly Callable _onData;
    private Vector2I _formatWindow;
    private FrameFormat? _format;
    private Rid _texture;

    private FrameGrabber(Viewport viewport, RenderingDevice device)
    {
        _viewport = viewport;
        _device = device;
        _onData = Callable.From<Variant>(OnData);
    }

    /// <summary>Frames requested whose bytes have not arrived yet.</summary>
    public int InFlight { get; private set; }

    /// <summary>A grabber for <paramref name="viewport"/>, or null without a rendering device (headless runs).</summary>
    public static FrameGrabber? Create(Viewport viewport)
    {
        RenderingDevice? device = RenderingServer.GetRenderingDevice();
        return device is null ? null : new FrameGrabber(viewport, device);
    }

    /// <summary>
    /// The current frame's format, or null with an error message: the texture is missing, or its
    /// layout is not 8-bit RGBA (for example with HDR 2D on).
    /// </summary>
    public FrameFormat? CurrentFormat(out string error)
    {
        Vector2I window = DisplayServer.WindowGetSize();
        if (_format is not null && window == _formatWindow)
        {
            error = "";
            return _format;
        }

        Rid texture = RenderingServer.TextureGetRdTexture(_viewport.GetTexture().GetRid());
        if (!texture.IsValid)
        {
            error = "no viewport texture";
            return null;
        }

        using RDTextureFormat format = _device.TextureGetFormat(texture);
        if (format.Format is not (RenderingDevice.DataFormat.R8G8B8A8Unorm or RenderingDevice.DataFormat.R8G8B8A8Srgb))
        {
            error = $"unsupported frame format {format.Format}";
            return null;
        }

        error = "";
        _formatWindow = window;
        _texture = texture;
        _format = new FrameFormat((int)format.Width, (int)format.Height, "rgba");
        return _format;
    }

    /// <summary>
    /// Asks for the frame's bytes, delivered to <paramref name="onFrame"/> in an array from
    /// <see cref="Buffers"/>: the first Width × Height × 4 bytes, top row first. The array may be
    /// longer; the receiver must return it to <see cref="Buffers"/>. <paramref name="argument"/> is handed back with it, so a
    /// caller needs no closure per frame. Call <see cref="CurrentFormat"/> first (it finds the texture).
    /// </summary>
    public void Request(Action<byte[], int> onFrame, int argument = 0)
    {
        InFlight++;
        _waiting.Enqueue((onFrame, argument));
        _device.TextureGetDataAsync(_texture, 0, _onData);
    }

    private void OnData(Variant data)
    {
        InFlight--;
        byte[] frame = CopyToBuffer(data);
        data.Dispose();
        (Action<byte[], int> onFrame, int argument) = _waiting.Dequeue();
        onFrame(frame, argument);
    }

    private static unsafe byte[] CopyToBuffer(Variant data)
    {
        godot_variant variant = data.CopyNativeVariant();
        godot_packed_byte_array bytes = NativeFuncs.godotsharp_variant_as_packed_byte_array(variant);
        try
        {
            byte[] frame = Buffers.Rent(bytes.Size);
            new ReadOnlySpan<byte>(bytes.Buffer, bytes.Size).CopyTo(frame);
            return frame;
        }
        finally
        {
            bytes.Dispose();
            variant.Dispose();
        }
    }
}
