using Godot;

namespace TheDeep;

/// <summary>
/// Windowed check of the owner capture keys (run with `-- --capture-test`; not part of
/// tools/check.sh because it needs a GPU and a window). Runs the player back and forth, measures
/// frame times without and with recording, presses F12 and F10 through the real input actions,
/// and checks the files were saved. Prints "CAPTURE TEST OK" and quits, or reports an error and
/// quits with code 1.
/// </summary>
public partial class CaptureTestDriver : Node
{
    // Phases in milliseconds of wall-clock time since the driver started.
    private const ulong BaselineStart = 1000;
    private const ulong ScreenshotAt = 3000;
    private const ulong RecordStart = 5000;
    private const ulong RecordEnd = 15000;
    private const ulong GiveUpAt = RecordEnd + 15000;

    private readonly FrameTimeStats _baseline = new();
    private readonly FrameTimeStats _recording = new();
    private readonly FrameTimeStats _baselineGpu = new();
    private readonly FrameTimeStats _recordingGpu = new();
    private readonly FrameTimeStats _baselineCpu = new();
    private readonly FrameTimeStats _recordingCpu = new();
    private ulong _startMsec;
    private ulong _lastMsec;
    private readonly DemoInput _demo = new();
    private string? _screenshot;
    private string? _video;

    public OwnerCapture Capture { get; set; } = null!;

    public CaptureTestDriver()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _Ready()
    {
        _startMsec = Time.GetTicksMsec();
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        Capture.Saved += OnSaved;
    }

    public override void _ExitTree()
    {
        Capture.Saved -= OnSaved;
    }

    public override void _Process(double delta)
    {
        ulong last = _lastMsec;
        ulong now = _lastMsec = Time.GetTicksMsec() - _startMsec;
        double ms = delta * 1000.0;
        Rid viewport = GetViewport().GetViewportRid();
        double gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);
        double cpu = RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport) + RenderingServer.GetFrameSetupTimeCpu();
        if (now is >= BaselineStart and < RecordStart)
        {
            _baseline.Add(ms);
            _baselineGpu.Add(gpu);
            _baselineCpu.Add(cpu);
        }
        else if (now >= RecordStart && last >= RecordStart && now < RecordEnd)
        {
            _recording.Add(ms);
            _recordingGpu.Add(gpu);
            _recordingCpu.Add(cpu);
        }

        if (Crossed(last, now, ScreenshotAt))
        {
            Press(InputActions.CaptureScreenshot);
        }
        else if (Crossed(last, now, RecordStart) || Crossed(last, now, RecordEnd))
        {
            Press(InputActions.CaptureVideo);
        }

        if (now > RecordEnd && _screenshot is not null && _video is not null)
        {
            Finish(null);
        }
        else if (Crossed(last, now, GiveUpAt))
        {
            Finish(_screenshot is null ? "no screenshot was saved" : "no video was saved");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _demo.Tick();
    }

    private static bool Crossed(ulong last, ulong now, ulong mark) => last < mark && now >= mark;

    private static void Press(StringName action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private void OnSaved(string path)
    {
        if (path.EndsWith(".png"))
        {
            _screenshot = path;
        }
        else if (path.EndsWith(".mp4"))
        {
            _video = path;
        }
    }

    private void Finish(string? problem)
    {
        GD.Print($"frame times without recording: {_baseline}");
        GD.Print($"frame times while recording:   {_recording}");
        GD.Print($"render GPU without / while recording: {_baselineGpu} / {_recordingGpu}");
        GD.Print($"render CPU without / while recording: {_baselineCpu} / {_recordingCpu}");
        GD.Print($"garbage collections: gen0 {System.GC.CollectionCount(0)}, gen1 {System.GC.CollectionCount(1)}, gen2 {System.GC.CollectionCount(2)}");
        if (problem is null)
        {
            GD.Print($"CAPTURE TEST OK: screenshot {_screenshot}, video {_video}");
            GetTree().Quit();
        }
        else
        {
            GD.PushError("CAPTURE TEST FAIL: " + problem);
            GetTree().Quit(1);
        }
    }
}
