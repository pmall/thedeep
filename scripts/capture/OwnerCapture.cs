using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// The owner's capture keys, in every build: F12 saves a screenshot, F10 starts and stops a video.
/// Files go to captures/ at the project root (next to the executable in exported builds), each
/// with a text file of the same name describing the game state, so "look at the last screenshot"
/// is enough to find and reproduce a report.
///
/// Frames are read back from the GPU asynchronously and encoded by ffmpeg in its own process, so
/// gameplay timing does not change while recording.
/// </summary>
public partial class OwnerCapture : CanvasLayer
{
    /// <summary>Most readbacks waiting at once; beyond that the video repeats a frame instead.</summary>
    private const int MaxFramesInFlight = 4;

    private static readonly TimeSpan FinishTimeout = TimeSpan.FromSeconds(10);

    [Export] public Player Player { get; set; } = null!;
    [Export] public World World { get; set; } = null!;

    /// <summary>Video frame rate. The game's own frame rate is unaffected.</summary>
    [Export] public int VideoFps { get; set; } = 60;

    /// <summary>Wider frames are scaled down to this width in the video.</summary>
    [Export] public int VideoMaxWidth { get; set; } = 1920;

    private readonly ConcurrentQueue<string> _messages = new();
    private readonly ConcurrentQueue<string> _savedFiles = new();
    private readonly HashSet<string> _reservedNames = new();
    private Control _recIndicator = null!;
    private Control _recDot = null!;
    private Label _recTime = null!;
    private Label _toast = null!;
    private Tween? _toastTween;
    private FrameGrabber? _grabber;
    private Task<VideoCodec?>? _codecProbe;
    private Recording? _recording;

    /// <summary>Raised on the main thread with the full path of each finished capture file (PNG or MP4).</summary>
    public event Action<string>? Saved;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _recIndicator = GetNode<Control>("%RecIndicator");
        _recDot = GetNode<Control>("%RecDot");
        _recTime = GetNode<Label>("%RecTime");
        _toast = GetNode<Label>("%Toast");
        _recIndicator.Visible = false;
        _toast.Modulate = Colors.Transparent;

        _grabber = FrameGrabber.Create(GetViewport());
        if (_grabber is not null)
        {
            _codecProbe = Task.Run(VideoEncoder.DetectCodec);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.CaptureScreenshot))
        {
            TakeScreenshot();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.CaptureVideo))
        {
            if (_recording is null)
            {
                StartRecording();
            }
            else
            {
                StopRecording("");
            }

            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        // Files are written on worker threads; report them here, on the main thread.
        while (_messages.TryDequeue(out string? message))
        {
            ShowToast(message);
        }

        while (_savedFiles.TryDequeue(out string? path))
        {
            Saved?.Invoke(path);
        }

        if (_recording is not null)
        {
            RecordFrame(_recording);
        }
    }

    public override void _ExitTree()
    {
        // Closing the window mid-recording: finish the file before the process ends.
        if (_recording is not null)
        {
            Recording recording = _recording;
            _recording = null;
            FinishRecording(recording, "game closed").Wait(FinishTimeout);
        }
    }

    private void TakeScreenshot()
    {
        FrameFormat? format = _grabber?.CurrentFormat(out _);
        if (_grabber is null || format is not { } frame)
        {
            ShowToast("Screenshot unavailable here");
            return;
        }

        string folder = CaptureFolder();
        string name = ReserveName(folder);
        string report = DescribeGame($"The Deep screenshot {name}.png").ToString();
        _grabber.Request(bytes => Task.Run(() => SaveScreenshot(bytes, frame, folder, name, report)));
    }

    private void SaveScreenshot(byte[] bytes, FrameFormat frame, string folder, string name, string report)
    {
        try
        {
            CreateFolder(folder);
            string png = Path.Combine(folder, name + ".png");
            Error error = FramePng.Save(bytes, frame, png);
            if (error != Error.Ok)
            {
                _messages.Enqueue($"Screenshot failed: {error}");
                return;
            }

            File.WriteAllText(Path.Combine(folder, name + ".txt"), report);
            _messages.Enqueue($"Screenshot saved   captures/{name}.png");
            _savedFiles.Enqueue(png);
        }
        catch (Exception e)
        {
            _messages.Enqueue($"Screenshot failed: {e.Message}");
        }
    }

    private void StartRecording()
    {
        if (_grabber is null || _codecProbe is null)
        {
            ShowToast("Video unavailable here");
            return;
        }

        if (!_codecProbe.IsCompleted)
        {
            ShowToast("Video not ready yet, try again in a second");
            return;
        }

        if (_codecProbe.Result is not { } codec)
        {
            ShowToast("Video needs ffmpeg, which was not found");
            return;
        }

        if (_grabber.CurrentFormat(out string formatError) is not { } format)
        {
            ShowToast($"Video unavailable: {formatError}");
            return;
        }

        string folder = CaptureFolder();
        string name = ReserveName(folder);
        VideoEncoder encoder;
        try
        {
            CreateFolder(folder);
            encoder = VideoEncoder.Start(new FfmpegCommand(
                format.Width, format.Height, format.PixelFormat, VideoFps, codec, VideoMaxWidth,
                Path.Combine(folder, name + ".mp4")), FrameGrabber.Buffers);
        }
        catch (Exception e)
        {
            ShowToast($"Video failed to start: {e.Message}");
            return;
        }

        CaptureReport report = DescribeGame($"The Deep video {name}.mp4");
        _recording = new Recording(encoder, new VideoClock(VideoFps, VideoFps), Time.GetTicksUsec(), format, folder, name, report);
        _recIndicator.Visible = true;
        RecordFrame(_recording);
    }

    private void StopRecording(string reason)
    {
        if (_recording is not { } recording)
        {
            return;
        }

        _recording = null;
        _recIndicator.Visible = false;
        if (reason.Length > 0)
        {
            ShowToast($"Recording stopped: {reason}");
        }

        _ = FinishRecording(recording, reason);
    }

    private void RecordFrame(Recording recording)
    {
        double elapsed = (Time.GetTicksUsec() - recording.StartUsec) / 1_000_000.0;
        _recDot.Modulate = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Cos((float)elapsed * Mathf.Tau * 0.5f));
        _recTime.Text = FormatDuration(elapsed);

        if (recording.Encoder.Failed)
        {
            StopRecording("ffmpeg stopped");
            return;
        }

        int due = recording.Clock.Advance(elapsed);
        if (due == 0)
        {
            return;
        }

        FrameFormat? format = _grabber!.CurrentFormat(out _);
        if (format != recording.Format)
        {
            StopRecording("the window size changed");
            return;
        }

        if (_grabber.InFlight >= MaxFramesInFlight)
        {
            recording.Encoder.CoverMissedFrames(due);
            return;
        }

        VideoEncoder encoder = recording.Encoder;
        _grabber.Request(bytes => encoder.Submit(bytes, due));
    }

    private async Task FinishRecording(Recording recording, string reason)
    {
        var (exitCode, errors) = await recording.Encoder.FinishAsync(FinishTimeout).ConfigureAwait(false);
        VideoEncoder encoder = recording.Encoder;
        string mp4 = Path.Combine(recording.Folder, recording.Name + ".mp4");
        double seconds = (double)encoder.FramesWritten / recording.Clock.Fps;

        recording.Report
            .Add("duration", string.Format(CultureInfo.InvariantCulture, "{0:0.0} s", seconds))
            .Add("video", $"{encoder.Command.Width}x{encoder.Command.Height} at {recording.Clock.Fps} fps, {FfmpegCommand.CodecName(encoder.Command.Codec)}, max width {encoder.Command.MaxWidth}")
            .Add("frames", $"{encoder.FramesWritten} written, {encoder.FramesRepeatedForLag} missed and repeated, {recording.Clock.FramesCut} cut by stalls")
            .Add("stopped", reason.Length > 0 ? reason : "F10");
        if (exitCode != 0)
        {
            recording.Report.Add("ffmpeg", $"exit code {exitCode}: {errors}");
        }

        try
        {
            File.WriteAllText(Path.Combine(recording.Folder, recording.Name + ".txt"), recording.Report.ToString());
        }
        catch (IOException)
        {
        }

        if (exitCode == 0)
        {
            _messages.Enqueue($"Video saved   captures/{recording.Name}.mp4   {FormatDuration(seconds)}");
            _savedFiles.Enqueue(mp4);
        }
        else
        {
            _messages.Enqueue($"Video failed: ffmpeg exit code {exitCode} (see captures/{recording.Name}.txt)");
        }
    }

    private void ShowToast(string text)
    {
        GD.Print($"[capture] {text}");
        _toast.Text = text;
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenProperty(_toast, CanvasItem.PropertyName.Modulate.ToString(), Colors.White, 0.25)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _toastTween.TweenInterval(2.5);
        _toastTween.TweenProperty(_toast, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, 0.8)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    /// <summary>Picks a free base name now, so two quick presses never collide while files are still being written.</summary>
    private string ReserveName(string folder)
    {
        string name = CaptureFiles.UniqueBaseName(DateTime.Now, n =>
            _reservedNames.Contains(n)
            || File.Exists(Path.Combine(folder, n + ".png"))
            || File.Exists(Path.Combine(folder, n + ".mp4"))
            || File.Exists(Path.Combine(folder, n + ".txt")));
        _reservedNames.Add(name);
        return name;
    }

    private CaptureReport DescribeGame(string title)
    {
        Vector3 position = Player.GlobalPosition;
        Vector3 velocity = Player.Velocity;
        Camera3D? camera = GetViewport().GetCamera3D();
        var abilities = new List<string>();
        foreach (Ability ability in AbilitySet.All)
        {
            if (Player.Abilities.Has(ability))
            {
                abilities.Add(ability.ToString());
            }
        }

        Vector2I window = DisplayServer.WindowGetSize();
        return new CaptureReport(title)
            .Add("time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Add("room", $"{World.Room.Name} ({World.Room.SceneFilePath})")
            .Add("position", FormatVector(position))
            .Add("velocity", FormatVector(velocity))
            .Add("state", Player.CurrentState.Name)
            .Add("facing", Player.Facing > 0 ? "right" : "left")
            .Add("on floor", Player.IsOnFloor() ? "yes" : "no")
            .Add("abilities", abilities.Count > 0 ? string.Join(", ", abilities) : "none")
            .Add("camera", camera is null ? "none" : FormatVector(camera.GlobalPosition))
            .Add("fps", Engine.GetFramesPerSecond().ToString("0", CultureInfo.InvariantCulture))
            .Add("window", $"{window.X}x{window.Y}")
            .Add("build", $"Godot {Engine.GetVersionInfo()["string"]}, {(OS.IsDebugBuild() ? "debug" : "release")}");
    }

    /// <summary>captures/ at the project root when run from source, next to the executable when exported.</summary>
    private static string CaptureFolder()
    {
        string root = OS.HasFeature("template")
            ? OS.GetExecutablePath().GetBaseDir()
            : ProjectSettings.GlobalizePath("res://");
        return Path.Combine(root, "captures");
    }

    /// <summary>Creates the capture folder with an empty .gdignore, so Godot never imports the captures.</summary>
    private static void CreateFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        string gdignore = Path.Combine(folder, ".gdignore");
        if (!File.Exists(gdignore))
        {
            File.WriteAllText(gdignore, "");
        }
    }

    private static string FormatVector(Vector3 v) =>
        string.Format(CultureInfo.InvariantCulture, "x {0:+0.00;-0.00}  y {1:+0.00;-0.00}  z {2:+0.00;-0.00}", v.X, v.Y, v.Z);

    private static string FormatDuration(double seconds)
    {
        int total = (int)seconds;
        return $"{total / 60}:{total % 60:00}";
    }

    private sealed record Recording(
        VideoEncoder Encoder, VideoClock Clock, ulong StartUsec, FrameFormat Format,
        string Folder, string Name, CaptureReport Report);
}
