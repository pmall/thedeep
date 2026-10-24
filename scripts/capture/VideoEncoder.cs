using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TheDeep;

/// <summary>
/// Pipes raw frames into an ffmpeg process from a background thread, so the game never waits on
/// the encoder. Godot-free; tested against the real ffmpeg.
///
/// The queue is short on purpose: when ffmpeg falls behind, new frames are not queued, and the
/// next frame written is repeated in their place instead. The video then stutters but keeps real
/// time, and memory stays bounded.
/// </summary>
public sealed class VideoEncoder
{
    /// <summary>Frames waiting for the writer thread; about 50 MB at 1080p.</summary>
    private const int QueueCapacity = 6;

    private readonly Process _process;
    private readonly ArrayPool<byte>? _pool;
    private readonly BlockingCollection<(byte[] Frame, int Repeats)> _queue = new(QueueCapacity);
    private readonly Thread _writer;
    private readonly StringBuilder _errors = new();
    private readonly object _addLock = new();
    private readonly int _frameBytes;
    private int _lagRepeats;
    private long _framesWritten;
    private long _framesRepeatedForLag;
    private bool _closed;
    private volatile bool _failed;

    private VideoEncoder(FfmpegCommand command, Process process, ArrayPool<byte>? pool)
    {
        Command = command;
        _process = process;
        _pool = pool;
        _frameBytes = command.Width * command.Height * 4;
        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "VideoEncoder writer" };
        _writer.Start();
    }

    public FfmpegCommand Command { get; }

    /// <summary>Frames sent to ffmpeg so far, repeats included.</summary>
    public long FramesWritten => Interlocked.Read(ref _framesWritten);

    /// <summary>Frames that were missed (ffmpeg or the readback was behind) and filled by a repeat.</summary>
    public long FramesRepeatedForLag => Interlocked.Read(ref _framesRepeatedForLag);

    /// <summary>True once ffmpeg stopped accepting frames (it crashed or rejected the input).</summary>
    public bool Failed => _failed;

    /// <summary>
    /// The best H.264 encoder that actually works on this machine, or null if ffmpeg is missing
    /// or cannot encode at all. Takes a fraction of a second: call it off the main thread.
    /// </summary>
    public static VideoCodec? DetectCodec()
    {
        foreach (VideoCodec codec in new[] { VideoCodec.Nvenc, VideoCodec.X264 })
        {
            try
            {
                var info = new ProcessStartInfo(FfmpegCommand.Executable)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (string arg in FfmpegCommand.ProbeArguments(codec))
                {
                    info.ArgumentList.Add(arg);
                }

                using Process probe = Process.Start(info)!;
                probe.StandardOutput.ReadToEndAsync();
                probe.StandardError.ReadToEndAsync();
                if (!probe.WaitForExit(10_000))
                {
                    probe.Kill();
                    continue;
                }

                if (probe.ExitCode == 0)
                {
                    return codec;
                }
            }
            catch (Win32Exception)
            {
                return null; // ffmpeg is not installed.
            }
        }

        return null;
    }

    /// <summary>
    /// Starts ffmpeg. Throws if it cannot be launched. Submitted frames are given back to
    /// <paramref name="pool"/>, when there is one, once they are written or dropped.
    /// </summary>
    public static VideoEncoder Start(FfmpegCommand command, ArrayPool<byte>? pool = null)
    {
        var info = new ProcessStartInfo(FfmpegCommand.Executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in command.Arguments())
        {
            info.ArgumentList.Add(arg);
        }

        Process process = Process.Start(info) ?? throw new InvalidOperationException("ffmpeg did not start");
        var encoder = new VideoEncoder(command, process, pool);
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += encoder.OnError;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return encoder;
    }

    /// <summary>
    /// Queues one raw frame (its first Width × Height × 4 bytes) to be written
    /// <paramref name="repeats"/> times. Never blocks. Ignored after <see cref="FinishAsync"/> or
    /// a failure.
    /// </summary>
    public void Submit(byte[] frame, int repeats)
    {
        lock (_addLock)
        {
            if (repeats > 0 && frame.Length >= _frameBytes && !_closed && !_failed)
            {
                if (_queue.TryAdd((frame, repeats)))
                {
                    return;
                }

                CoverMissedFrames(repeats);
            }
        }

        _pool?.Return(frame);
    }

    /// <summary>
    /// Fills <paramref name="count"/> video frames whose image never arrived, by repeating the
    /// next frame written. Keeps the video in real time when frames are missed.
    /// </summary>
    public void CoverMissedFrames(int count)
    {
        if (count > 0)
        {
            Interlocked.Add(ref _lagRepeats, count);
        }
    }

    /// <summary>
    /// Writes what is queued, closes ffmpeg's input and waits for the file to be finished.
    /// Returns ffmpeg's exit code and error output.
    /// </summary>
    public async Task<(int ExitCode, string Errors)> FinishAsync(TimeSpan timeout)
    {
        lock (_addLock)
        {
            if (!_closed)
            {
                _closed = true;
                _queue.CompleteAdding();
            }
        }

        await Task.Run(() => _writer.Join(timeout)).ConfigureAwait(false);
        using var cancel = new CancellationTokenSource(timeout);
        try
        {
            await _process.WaitForExitAsync(cancel.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _process.Kill();
            return (-1, "ffmpeg did not finish in time and was killed");
        }

        lock (_errors)
        {
            return (_process.ExitCode, _errors.ToString().Trim());
        }
    }

    private void WriteLoop()
    {
        Stream stdin = _process.StandardInput.BaseStream;
        try
        {
            foreach (var (frame, repeats) in _queue.GetConsumingEnumerable())
            {
                int lagRepeats = Interlocked.Exchange(ref _lagRepeats, 0);
                int total = repeats + lagRepeats;
                for (int i = 0; i < total; i++)
                {
                    stdin.Write(frame, 0, _frameBytes);
                }

                Interlocked.Add(ref _framesWritten, total);
                Interlocked.Add(ref _framesRepeatedForLag, lagRepeats);
                _pool?.Return(frame);
            }

            stdin.Close();
        }
        catch (IOException)
        {
            // ffmpeg exited early; FinishAsync reports its errors.
            _failed = true;
            try
            {
                stdin.Close();
            }
            catch (IOException)
            {
            }
        }
    }

    private void OnError(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        lock (_errors)
        {
            _errors.AppendLine(e.Data);
        }
    }
}
