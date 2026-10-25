using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// Look-dev run for tools/capture.sh (`-- --lookdev &lt;dir&gt;`). Renders at a fixed size (1080p by
/// default) whatever the window size, without HUD:
/// 1. every <see cref="PhotoSpot"/> of the room to &lt;dir&gt;/&lt;Room&gt;-&lt;Spot&gt;.png;
/// 2. the room's reels (<see cref="MotionReel"/>): scripted moves shot close, a frame per shot to
///    &lt;dir&gt;/&lt;reel folder&gt;/ (tools/motion_sheet.py makes them a contact sheet);
/// 3. a scripted run with the gameplay camera, measuring frame times into &lt;dir&gt;/&lt;PerfFile&gt;.
/// The room's machines are reset before every shot and segment, so they act the same from run to run.
/// Meant to run with `--fixed-fps 60 --disable-vsync`, so game time is identical from run to run
/// and frame times show the real cost. Prints "LOOKDEV OK" and quits, or quits with code 1.
/// </summary>
public partial class LookDevDriver : Node
{
    /// <summary>Frames to wait at each spot: the player lands, turns, and the image settles.</summary>
    private const int SettleFrames = 45;

    private const int WarmupFrames = 120;
    private const int MeasuredFrames = 900;

    /// <summary>Frame time the game must stay under at 1080p on the owner's RTX 3060 (docs/roadmap.md, M0).</summary>
    private const double BudgetMs = 10.0;

    private readonly ConcurrentQueue<string> _problems = new();
    private readonly List<string> _saved = new();
    private readonly FrameTimeStats _frame = new();
    private readonly FrameTimeStats _gpu = new();
    private readonly FrameTimeStats _cpu = new();
    private IEnumerator<int> _script = null!;
    private FrameGrabber? _grabber;
    private DemoInput? _demo;
    private MotionReel.Segment? _segment;
    private int _segmentTick;
    private int _pendingSaves;
    private ulong _lastFrameUsec;
    private bool _measuring;

    public Player Player { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public SideCamera Camera { get; set; } = null!;
    public MachineDirector Machines { get; set; } = null!;

    /// <summary>Layers hidden during the run (HUD, capture overlay).</summary>
    public CanvasLayer[] Overlays { get; set; } = Array.Empty<CanvasLayer>();

    public string OutputDir { get; set; } = "";

    /// <summary>File name of the frame times in <see cref="OutputDir"/>.</summary>
    public string PerfFile { get; set; } = "perf.json";
    public Vector2I RenderSize { get; set; } = new(1920, 1080);

    public LookDevDriver()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _Ready()
    {
        Window root = GetTree().Root;
        root.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
        root.ContentScaleAspect = Window.ContentScaleAspectEnum.Ignore;
        root.ContentScaleSize = RenderSize;
        foreach (CanvasLayer overlay in Overlays)
        {
            overlay.Visible = false;
        }

        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        _grabber = FrameGrabber.Create(GetViewport());
        _script = Run().GetEnumerator();
    }

    public override void _PhysicsProcess(double delta)
    {
        _demo?.Tick();
        if (_segment is not null)
        {
            foreach (MotionReel.Press press in _segment.Presses)
            {
                if (press.Tick == _segmentTick && press.Down)
                {
                    Input.ActionPress(press.Action);
                }
                else if (press.Tick == _segmentTick)
                {
                    Input.ActionRelease(press.Action);
                }
            }

            _segmentTick++;
        }
    }

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksUsec();
        if (_measuring)
        {
            Rid viewport = GetViewport().GetViewportRid();
            _frame.Add((now - _lastFrameUsec) / 1000.0);
            _gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport));
            _cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport) + RenderingServer.GetFrameSetupTimeCpu());
        }

        _lastFrameUsec = now;
        _script.MoveNext();
    }

    private IEnumerable<int> Run()
    {
        if (_grabber is null)
        {
            Finish("no rendering device (headless?)");
            yield break;
        }

        Directory.CreateDirectory(OutputDir);
        yield return 0; // let the new render size apply

        if (_grabber.CurrentFormat(out string error) is not { } format)
        {
            Finish(error);
            yield break;
        }

        if (format.Width != RenderSize.X || format.Height != RenderSize.Y)
        {
            Finish($"rendering at {format.Width}x{format.Height} instead of {RenderSize.X}x{RenderSize.Y}");
            yield break;
        }

        // 1. Photo spots.
        foreach (PhotoSpot spot in Room.PhotoSpots())
        {
            Player.Respawn(Room.ToGlobal(spot.PlayerPosition), spot.PlayerFacing);
            Machines.ResetMachines();
            Camera.FrameShot(spot.GlobalPosition, spot.Fov);
            for (int i = 0; i < SettleFrames; i++)
            {
                yield return 0;
            }

            // A request made now returns the last finished frame: the settled shot.
            string path = Path.Combine(OutputDir, $"{Room.Name}-{spot.Name}.png");
            Interlocked.Increment(ref _pendingSaves);
            _grabber.Request(bytes => Task.Run(() => Save(bytes, format, path)));
            _saved.Add(Path.GetFileName(path));
        }

        if (_saved.Count == 0)
        {
            _problems.Enqueue($"room {Room.Name} has no PhotoSpot");
        }

        // 2. Reels, shot with a close camera.
        foreach (MotionReel.Reel reel in MotionReel.For(Room.Name))
        {
            string reelDir = Path.Combine(OutputDir, reel.Folder);
            Directory.CreateDirectory(reelDir);
            int shotCount = 0;
            foreach (MotionReel.Segment segment in reel.Segments)
            {
                DemoInput.ReleaseAll();
                Player.Respawn(Room.ToGlobal(segment.Start), segment.Facing);
                Machines.ResetMachines();
                Machine? subject = segment.Subject is { } name ? Room.FindChild(name, true, false) as Machine : null;
                if (segment.Subject is not null && subject is null)
                {
                    _problems.Enqueue($"reel {reel.Folder}: no machine named {segment.Subject} in {Room.Name}");
                }

                _segment = segment;
                _segmentTick = 0;
                while (_segmentTick < segment.Length)
                {
                    if (segment.Camera is { } fixedCamera)
                    {
                        Camera.FrameShot(Room.ToGlobal(fixedCamera), 0f);
                    }
                    else
                    {
                        Vector3 p = subject?.GetGlobalTransformInterpolated().Origin ?? Player.GetGlobalTransformInterpolated().Origin;
                        float lift = subject is null ? 0.9f : 0.4f;
                        Camera.FrameShot(new Vector3(p.X, p.Y + lift, reel.CameraDistance), 0f);
                    }

                    foreach (MotionReel.Shot shot in segment.Shots)
                    {
                        if (shot.Tick == _segmentTick)
                        {
                            string path = Path.Combine(reelDir, $"{shotCount++:00}-{shot.Label.Replace(' ', '-')}.png");
                            Interlocked.Increment(ref _pendingSaves);
                            _grabber.Request(bytes => Task.Run(() => Save(bytes, format, path)));
                        }
                    }

                    yield return 0;
                }
            }

            _segment = null;
            DemoInput.ReleaseAll();
        }

        // 3. Frame times over a scripted run with the gameplay camera.
        Player.Respawn(Room.SpawnPoint.GlobalPosition, 1);
        Machines.ResetMachines();
        Camera.ResumeFollow();
        _demo = new DemoInput();
        for (int i = 0; i < WarmupFrames; i++)
        {
            yield return 0;
        }

        _measuring = true;
        for (int i = 0; i < MeasuredFrames; i++)
        {
            yield return 0;
        }

        _measuring = false;
        _demo = null;
        DemoInput.ReleaseAll();

        while (Volatile.Read(ref _pendingSaves) > 0)
        {
            yield return 0;
        }

        WritePerf(format);
        Finish(null);
    }

    private void Save(byte[] bytes, FrameFormat format, string path)
    {
        try
        {
            Error error = FramePng.Save(bytes, format, path);
            if (error != Error.Ok)
            {
                _problems.Enqueue($"could not save {path}: {error}");
            }
        }
        catch (Exception e)
        {
            _problems.Enqueue($"could not save {path}: {e.Message}");
        }
        finally
        {
            Interlocked.Decrement(ref _pendingSaves);
        }
    }

    private void WritePerf(FrameFormat format)
    {
        var perf = new Dictionary<string, object>
        {
            ["size"] = $"{format.Width}x{format.Height}",
            ["budget_ms"] = BudgetMs,
            ["frame"] = Summary(_frame),
            ["gpu"] = Summary(_gpu),
            ["cpu"] = Summary(_cpu),
            ["frames"] = _frame.Count,
            ["gpu_name"] = RenderingServer.GetVideoAdapterName(),
            ["godot"] = (string)Engine.GetVersionInfo()["string"],
        };
        File.WriteAllText(Path.Combine(OutputDir, PerfFile), JsonSerializer.Serialize(perf, new JsonSerializerOptions { WriteIndented = true }));

        bool withinBudget = _frame.Average <= BudgetMs && _frame.Percentile(99) <= BudgetMs;
        GD.Print($"frame {_frame}");
        GD.Print($"gpu   {_gpu}");
        GD.Print($"cpu   {_cpu}");
        GD.Print(withinBudget
            ? $"within the {BudgetMs:0} ms budget"
            : $"OVER the {BudgetMs:0} ms budget (average and p99 must stay under it)");
    }

    private static Dictionary<string, double> Summary(FrameTimeStats stats) => new()
    {
        ["avg"] = Math.Round(stats.Average, 3),
        ["p99"] = Math.Round(stats.Percentile(99), 3),
        ["worst"] = Math.Round(stats.Worst, 3),
    };

    private void Finish(string? problem)
    {
        if (problem is not null)
        {
            _problems.Enqueue(problem);
        }

        if (_problems.IsEmpty)
        {
            GD.Print(string.Create(CultureInfo.InvariantCulture, $"LOOKDEV OK: {_saved.Count} spots ({string.Join(", ", _saved)}) in {OutputDir}"));
            GetTree().Quit();
        }
        else
        {
            GD.PushError("LOOKDEV FAIL: " + string.Join("; ", _problems));
            GetTree().Quit(1);
        }
    }
}
