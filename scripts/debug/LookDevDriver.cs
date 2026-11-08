using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    /// <summary>Frame time the game must stay under at 1080p on the owner's RTX 3060 (docs/principles.md#performance).</summary>
    private const double BudgetMs = 10.0;

    /// <summary>How the perf report is written: indented, for people to read.</summary>
    private static readonly JsonSerializerOptions PerfJson = new() { WriteIndented = true };

    private readonly ConcurrentQueue<string> _problems = new();
    private readonly List<string> _saved = new();
    private readonly FrameTimeStats _frame = new();
    private readonly FrameTimeStats _gpu = new();
    private readonly FrameTimeStats _cpu = new();
    private readonly FrameTimeStats _process = new();
    private readonly FrameTimeStats _physics = new();

    /// <summary>Per measured frame: frame, GPU, render CPU, process and physics script times (ms) and the GCs since the last frame.</summary>
    private readonly List<(double Frame, double Gpu, double Cpu, double Process, double Physics, int Gc0, int Gc1, int Gc2)> _frames = new();
    private int _gc0, _gc1, _gc2;

    /// <summary>Per measured frame, when THEDEEP_PAINT_TIMING=1: the paint pass's GPU ms per step (PaintPass.StepNames).</summary>
    private readonly List<double[]> _paintSteps = new();
    private readonly bool _paintTiming = PaintPass.Timed = System.Environment.GetEnvironmentVariable("THEDEEP_PAINT_TIMING") == "1";
    private IEnumerator<int>? _script;
    private FrameGrabber? _grabber;
    private DemoInput? _demo;
    private MotionReel.Segment? _segment;
    private int _segmentTick;
    private int _pendingSaves;
    private ulong _lastFrameUsec;
    private bool _measuring;
    private long _allocatedAtStart, _allocated;
    private readonly AllocationSampler? _sampler = System.Environment.GetEnvironmentVariable("THEDEEP_ALLOC_SAMPLE") == "1" ? new() : null;

    public required Player Player { get; init; }
    public required Room Room { get; init; }
    public required SideCamera Camera { get; init; }
    public required MachineDirector Machines { get; init; }

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
            _process.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0);
            _physics.Add(Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0);
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            _frames.Add(((now - _lastFrameUsec) / 1000.0, RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport),
                RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport) + RenderingServer.GetFrameSetupTimeCpu(),
                Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0, Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0,
                gc0 - _gc0, gc1 - _gc1, gc2 - _gc2));
            if (_paintTiming)
            {
                _paintSteps.Add((double[])PaintPass.StepMs.Clone());
            }
        }

        (_gc0, _gc1, _gc2) = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));

        _lastFrameUsec = now;
        (_script ??= Run().GetEnumerator()).MoveNext();
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
            foreach (CanvasLayer overlay in Overlays)
            {
                overlay.Visible = spot.ShowHud && overlay is HealthHud;
            }

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
            Player.Abilities.Set(reel.Abilities, true);
            foreach (string item in reel.Items ?? Array.Empty<string>())
            {
                Player.Items.Take(item);
            }

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
            Player.Abilities.Set(reel.Abilities, false);
        }

        // 3. The camera settling after a run, every frame compared with the one before.
        foreach (MotionReel.Settle settle in MotionReel.SettlesFor(Room.Name))
        {
            foreach (int step in SettleRun(settle, format))
            {
                yield return step;
            }
        }

        // 4. Frame times over a scripted run with the gameplay camera.
        Player.Respawn(Room.SpawnPoint.GlobalPosition, 1);
        Machines.ResetMachines();
        Camera.ResumeFollow();
        _demo = new DemoInput();
        for (int i = 0; i < WarmupFrames; i++)
        {
            yield return 0;
        }

        _measuring = true;
        _allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
        if (_sampler is not null)
        {
            _sampler.FollowThisThread();
            _sampler.Active = true;
        }
        for (int i = 0; i < MeasuredFrames; i++)
        {
            yield return 0;
        }

        _allocated = GC.GetAllocatedBytesForCurrentThread() - _allocatedAtStart;
        if (_sampler is not null)
        {
            _sampler.Active = false;
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

    /// <summary>
    /// A settle reel (docs/testing.md#settle-reel). The pilgrim runs, lets go, and the gameplay camera eases to a stop:
    /// how many frames it creeps by under a pixel is counted. Then the world is frozen (time scale 0: no animation, no
    /// particle, no shader time) and the camera is held still, then swept a sixteenth of a pixel a frame, right then up,
    /// every frame compared with the one before (<see cref="FrameShimmer"/>). Writes settle-&lt;Room&gt;.json,
    /// settle-&lt;Room&gt;.png (the settled frame) and settle-&lt;Room&gt;-heat.png (each pixel's largest change in the
    /// sweep, ×4: where the image pops), and prints a `settle` line.
    /// </summary>
    private IEnumerable<int> SettleRun(MotionReel.Settle settle, FrameFormat format)
    {
        const int MaxSettle = 900, StillFrames = 16, SweepFrames = 48, FreezeFrames = 12;
        DemoInput.ReleaseAll();
        Player.Respawn(Room.ToGlobal(settle.Start), settle.Facing);
        Machines.ResetMachines();
        Camera.ResumeFollow();
        for (int i = 0; i < SettleFrames; i++)
        {
            yield return 0;
        }

        double pixelsPerMeter = format.Height / (2.0 * CameraMath.HalfHeightAtDistance(Camera.Distance, Camera.Fov));
        StringName run = settle.Facing > 0 ? InputActions.MoveRight : InputActions.MoveLeft;
        Input.ActionPress(run);
        for (int tick = 0; tick < settle.RunTicks; tick++)
        {
            yield return 0;
        }

        Input.ActionRelease(run);
        int creeping = 0, still = 0, settled = 0;
        Vector3 last = Camera.GlobalPosition;
        for (; settled < MaxSettle && still < 30; settled++)
        {
            yield return 0;
            double shift = Camera.GlobalPosition.DistanceTo(last) * pixelsPerMeter;
            last = Camera.GlobalPosition;
            creeping += FrameShimmer.Creeps(shift) ? 1 : 0;
            still = shift < FrameShimmer.StillShift ? still + 1 : 0;
        }

        // Frozen: from here only the camera moves.
        Engine.TimeScale = 0.0;
        Vector3 rest = Camera.GlobalPosition;
        Camera.FrameShot(rest, 0f);
        for (int i = 0; i < FreezeFrames; i++)
        {
            yield return 0;
        }

        int pixels = format.Width * format.Height;
        var stillPhase = new FrameShimmer.Phase();
        var sweepPhase = new FrameShimmer.Phase();
        byte[] heat = new byte[pixels];
        byte[]? previous = null;
        int pending = 0;
        float step = (float)(FrameShimmer.SweepStep / pixelsPerMeter);
        for (int frame = 0; frame <= StillFrames + 2 * SweepFrames; frame++)
        {
            int sweep = frame - StillFrames;
            Vector3 at = sweep <= 0 ? rest
                : sweep <= SweepFrames ? rest + new Vector3(sweep * step, 0f, 0f)
                : rest + new Vector3(SweepFrames * step, (sweep - SweepFrames) * step, 0f);
            Camera.FrameShot(at, 0f);
            yield return 0;

            // A request returns the last finished frame: the one drawn from the camera set a frame ago.
            FrameShimmer.Phase? phase = frame == 0 ? null : sweep <= 0 ? stillPhase : sweepPhase;
            pending++;
            // Frames arrive in the order asked for, one at a time.
            _grabber!.Request(bytes =>
            {
                if (previous is not null && phase is not null)
                {
                    phase.Add(FrameShimmer.Compare(previous, bytes, pixels, phase == sweepPhase ? heat : Span<byte>.Empty));
                }

                if (previous is not null)
                {
                    FrameGrabber.Buffers.Return(previous);
                }

                previous = bytes;
                pending--;
            });
        }

        while (pending > 0)
        {
            yield return 0;
        }

        Engine.TimeScale = 1.0;
        string name = $"settle-{Room.Name}";
        FramePng.Save(previous!, format, Path.Combine(OutputDir, name + ".png"));
        for (int i = 0; i < pixels; i++)
        {
            heat[i] = (byte)Math.Min(255, heat[i] * 4);
        }

        using (Image heatImage = Image.CreateFromData(format.Width, format.Height, false, Image.Format.L8, heat))
        {
            heatImage.SavePng(Path.Combine(OutputDir, name + "-heat.png"));
        }

        static Dictionary<string, double> Of(FrameShimmer.Phase phase) => new()
        {
            ["frames"] = phase.Frames,
            ["mean_change"] = Math.Round(phase.MeanDifference, 3),
            ["pop_percent"] = Math.Round(phase.FlickerShare * 100.0, 4),
        };
        File.WriteAllText(Path.Combine(OutputDir, name + ".json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["size"] = $"{format.Width}x{format.Height}",
            ["pop_threshold"] = FrameShimmer.FlickerThreshold,
            ["sweep_step_px"] = FrameShimmer.SweepStep,
            ["settle_frames"] = settled,
            ["creep_frames"] = creeping,
            ["still"] = Of(stillPhase),
            ["sweep"] = Of(sweepPhase),
        }, PerfJson));
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"settle {Room.Name}: creeps {creeping} frames; sweep pops {sweepPhase.FlickerShare * 100.0:0.0000}%, change {sweepPhase.MeanDifference:0.000}; still pops {stillPhase.FlickerShare * 100.0:0.0000}%, change {stillPhase.MeanDifference:0.000}"));
        DemoInput.ReleaseAll();
        Camera.ResumeFollow();
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
            ["process"] = Summary(_process),
            ["physics"] = Summary(_physics),
            ["gc"] = new Dictionary<string, int>
            {
                ["gen0"] = _frames.Sum(f => f.Gc0),
                ["gen1"] = _frames.Sum(f => f.Gc1),
                ["gen2"] = _frames.Sum(f => f.Gc2),
            },
            // Managed bytes allocated on the main thread (game logic and physics) per frame of the run.
            ["alloc_kb_per_frame"] = Math.Round(_allocated / 1024.0 / Math.Max(1, _frame.Count), 2),
            // What allocated during the run (sampled about every 100 KB, all threads).
            ["alloc_types"] = _sampler?.Top(12) ?? new[] { "not sampled (THEDEEP_ALLOC_SAMPLE=1 samples)" },
            // The slowest frames with what else happened in them, and in the frame before (monitors lag a frame).
            ["slowest"] = Enumerable.Range(0, _frames.Count).OrderByDescending(i => _frames[i].Frame).Take(12).OrderBy(i => i)
                .Select(i => string.Create(CultureInfo.InvariantCulture,
                    $"#{i} frame {_frames[i].Frame:0.0} gpu {_frames[i].Gpu:0.0} cpu {_frames[i].Cpu:0.0} process {_frames[i].Process:0.0} physics {_frames[i].Physics:0.0} gc {_frames[i].Gc0}/{_frames[i].Gc1}/{_frames[i].Gc2}"))
                .ToArray(),
            // The slowest frames by GPU time; with THEDEEP_PAINT_TIMING=1, the paint steps' ms (they lag the frame by one or two).
            ["slowest_gpu"] = Enumerable.Range(0, _frames.Count).OrderByDescending(i => _frames[i].Gpu).Take(12).OrderBy(i => i)
                .Select(i => string.Create(CultureInfo.InvariantCulture, $"#{i} gpu {_frames[i].Gpu:0.0}")
                    + (_paintTiming ? " paint " + string.Join(" ", Enumerable.Range(Math.Max(0, i - 1), Math.Min(3, _paintSteps.Count - Math.Max(0, i - 1)))
                        .Select(j => "[" + string.Join(" ", _paintSteps[j].Select(ms => ms.ToString("0.0", CultureInfo.InvariantCulture))) + "]")) : ""))
                .ToArray(),
            ["paint_steps_avg_ms"] = _paintTiming && _paintSteps.Count > 0
                ? Enumerable.Range(0, PaintPass.StepNames.Length).ToDictionary(s => PaintPass.StepNames[s], s => Math.Round(_paintSteps.Average(f => f[s]), 3))
                : new Dictionary<string, double>(),
            ["frames"] = _frame.Count,
            ["gpu_name"] = RenderingServer.GetVideoAdapterName(),
            ["godot"] = (string)Engine.GetVersionInfo()["string"],
        };
        File.WriteAllText(Path.Combine(OutputDir, PerfFile), JsonSerializer.Serialize(perf, PerfJson));

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

        _sampler?.Dispose();
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
