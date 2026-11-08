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
/// 3. the room's settle reels (<see cref="SettleRun"/>);
/// 4. a scripted run with the gameplay camera, measuring frame times into &lt;dir&gt;/&lt;PerfFile&gt;.
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

    /// <summary>Only the room's settle reels (`--still`, tools/still.sh): no photo spot, reel or frame times.</summary>
    public bool StillOnly { get; set; }

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

        if (StillOnly)
        {
            foreach (MotionReel.Settle settle in MotionReel.SettlesFor(Room.Name))
            {
                foreach (int step in SettleRun(settle, format))
                {
                    yield return step;
                }
            }

            Finish(null);
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
    /// A settle reel (docs/testing.md#settle-reel). The pilgrim runs, lets go, and the gameplay camera eases to a stop; its
    /// path is recorded, and the frames it creeps by under a pixel are counted. Then, every frame compared with the one
    /// before (<see cref="FrameShimmer"/>):
    /// 1. live: the world runs and the camera rests where it stopped; a pixel that jumps and comes back blinks;
    /// 2. settle: the world frozen (time scale 0: no animation, no particle, no shader time), the camera replays its path
    ///    to rest, so only the camera moves, exactly as it moved in play;
    /// 3. still: the camera held at rest;
    /// 4. sweep: the camera stepped a sixteenth of a pixel a frame, right then up.
    /// In 2–4 a pixel that changes more than the camera's step allows pops. Writes settle-&lt;Room&gt;.json (each phase:
    /// frames, mean luma change, pops or blinks, the worst frame's), settle-&lt;Room&gt;.png (the settled frame) and
    /// settle-&lt;Room&gt;-heat.png (that frame darkened, every pixel that popped red, every one that blinked cyan), and
    /// prints a `settle` line.
    /// </summary>
    private IEnumerable<int> SettleRun(MotionReel.Settle settle, FrameFormat format)
    {
        const int MaxSettle = 900, LiveFrames = 90, StillFrames = 16, SweepFrames = 48, FreezeFrames = 30;
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
        var path = new List<Vector3> { Camera.GlobalPosition };
        for (; settled < MaxSettle && still < 30; settled++)
        {
            yield return 0;
            double shift = Camera.GlobalPosition.DistanceTo(path[^1]) * pixelsPerMeter;
            path.Add(Camera.GlobalPosition);
            creeping += FrameShimmer.Creeps(shift) ? 1 : 0;
            still = shift < FrameShimmer.StillShift ? still + 1 : 0;
        }

        var meter = new SettleMeter(format.Width, format.Height);

        // 1. Live: the world runs, the camera rests.
        for (int i = 0; i <= LiveFrames; i++)
        {
            yield return 0;
            _grabber!.Request(bytes => meter.Add(bytes, meter.Live, 0.0));
        }

        // Frozen: from here only the camera moves.
        Engine.TimeScale = 0.0;
        Vector3 rest = path[^1];
        Camera.FrameShot(path[0], 0f);
        for (int i = 0; i < FreezeFrames; i++)
        {
            yield return 0;
        }

        // 2. Settle: the recorded path replayed. A request returns the frame drawn from the camera set before the yield.
        meter.Restart();
        for (int i = 0; i < path.Count; i++)
        {
            Camera.FrameShot(path[i], 0f);
            double step = i == 0 ? 0.0 : path[i].DistanceTo(path[i - 1]) * pixelsPerMeter;
            yield return 0;
            _grabber!.Request(bytes => meter.Add(bytes, meter.Settle, step));
        }

        // 3. Still, then 4. the sweep.
        float sweepMeters = (float)(FrameShimmer.SweepStep / pixelsPerMeter);
        for (int frame = 0; frame < StillFrames + 2 * SweepFrames; frame++)
        {
            int sweep = frame - StillFrames + 1;
            Vector3 at = sweep <= 0 ? rest
                : sweep <= SweepFrames ? rest + new Vector3(sweep * sweepMeters, 0f, 0f)
                : rest + new Vector3(SweepFrames * sweepMeters, (sweep - SweepFrames) * sweepMeters, 0f);
            Camera.FrameShot(at, 0f);
            yield return 0;
            FrameShimmer.Phase phase = sweep <= 0 ? meter.Still : meter.Sweep;
            double step = sweep <= 0 ? 0.0 : FrameShimmer.SweepStep;
            _grabber!.Request(bytes => meter.Add(bytes, phase, step));
        }

        while (_grabber!.InFlight > 0)
        {
            yield return 0;
        }

        Engine.TimeScale = 1.0;
        string name = $"settle-{Room.Name}";
        meter.Save(format, Path.Combine(OutputDir, name + ".png"), Path.Combine(OutputDir, name + "-heat.png"));
        static Dictionary<string, double> Of(FrameShimmer.Phase phase) => new()
        {
            ["frames"] = phase.Frames,
            ["mean_change"] = Math.Round(phase.MeanDifference, 3),
            ["pops"] = phase.Pops,
            ["worst_frame"] = phase.WorstFrame,
        };
        long pops = meter.Settle.Pops + meter.Still.Pops + meter.Sweep.Pops;
        File.WriteAllText(Path.Combine(OutputDir, name + ".json"), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["size"] = $"{format.Width}x{format.Height}",
            ["tolerance"] = FrameShimmer.Tolerance,
            ["slack"] = FrameShimmer.Slack,
            ["blink_threshold"] = FrameShimmer.BlinkThreshold,
            ["sweep_step_px"] = FrameShimmer.SweepStep,
            ["settle_frames"] = settled,
            ["creep_frames"] = creeping,
            ["pops"] = pops,
            ["blinks"] = meter.Live.Pops,
            ["live"] = Of(meter.Live),
            ["settle"] = Of(meter.Settle),
            ["still"] = Of(meter.Still),
            ["sweep"] = Of(meter.Sweep),
        }, PerfJson));
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"settle {Room.Name}: creeps {creeping} frames; pops {pops} (settle {meter.Settle.Pops}, still {meter.Still.Pops}, sweep {meter.Sweep.Pops}; worst frame {Math.Max(meter.Settle.WorstFrame, Math.Max(meter.Still.WorstFrame, meter.Sweep.WorstFrame))}); blinks {meter.Live.Pops} (worst frame {meter.Live.WorstFrame})"));
        DemoInput.ReleaseAll();
        Camera.ResumeFollow();
    }

    /// <summary>
    /// The frames of a settle reel as they arrive (in the order asked for): each turned to luma and compared with the one
    /// before (<see cref="FrameShimmer"/>), the pops and blinks kept per pixel for the heat map, the last frame kept.
    /// </summary>
    private sealed class SettleMeter
    {
        public readonly FrameShimmer.Phase Live = new();
        public readonly FrameShimmer.Phase Settle = new();
        public readonly FrameShimmer.Phase Still = new();
        public readonly FrameShimmer.Phase Sweep = new();

        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _pops;
        private readonly byte[] _blinks;
        private byte[] _older;
        private byte[] _previous;
        private byte[] _current;
        private int _frames;
        private double _lastStep;
        private byte[]? _last;

        public SettleMeter(int width, int height)
        {
            _width = width;
            _height = height;
            int pixels = width * height;
            _pops = new byte[pixels];
            _blinks = new byte[pixels];
            _older = new byte[pixels];
            _previous = new byte[pixels];
            _current = new byte[pixels];
        }

        /// <summary>Forgets the frames before: the next one starts a new sequence (the camera jumped).</summary>
        public void Restart()
        {
            _frames = 0;
            _lastStep = 0.0;
        }

        /// <summary>
        /// Takes a frame (returned to <see cref="FrameGrabber.Buffers"/> later) whose camera moved <paramref name="step"/>
        /// screen pixels since the last; <see cref="Live"/> counts blinks, any other phase pops. As a request may return a
        /// frame drawn a frame late, the camera step allowed is the larger of this one and the last.
        /// </summary>
        public void Add(byte[] rgba, FrameShimmer.Phase phase, double step)
        {
            int pixels = _width * _height;
            FrameShimmer.Luma(rgba, _current.AsSpan(0, pixels));
            if (phase == Live && _frames >= 2)
            {
                phase.Add(FrameShimmer.Blinks(_older, _previous, _current, pixels, _blinks));
            }
            else if (phase != Live && _frames >= 1)
            {
                phase.Add(FrameShimmer.Compare(_previous, _current, _width, _height, Math.Max(step, _lastStep), _pops));
            }

            (_older, _previous, _current) = (_previous, _current, _older);
            _frames++;
            _lastStep = step;
            if (_last is not null)
            {
                FrameGrabber.Buffers.Return(_last);
            }

            _last = rgba;
        }

        /// <summary>Saves the last frame to <paramref name="framePath"/> and the heat map over it to <paramref name="heatPath"/>.</summary>
        public void Save(FrameFormat format, string framePath, string heatPath)
        {
            if (_last is not { } last)
            {
                return;
            }

            int pixels = _width * _height;
            byte[] heat = new byte[pixels * 3];
            for (int i = 0; i < pixels; i++)
            {
                byte shade = (byte)(_previous[i] * 0.3);
                (byte r, byte g, byte b) = _pops[i] > 0 ? ((byte)255, (byte)40, (byte)40)
                    : _blinks[i] > 0 ? ((byte)40, (byte)220, (byte)255)
                    : (shade, shade, shade);
                heat[i * 3] = r;
                heat[i * 3 + 1] = g;
                heat[i * 3 + 2] = b;
            }

            using (Image heatImage = Image.CreateFromData(_width, _height, false, Image.Format.Rgb8, heat))
            {
                heatImage.SavePng(heatPath);
            }

            _last = null;
            FramePng.Save(last, format, framePath);
        }
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
            // The slowest frames by GPU time.
            ["slowest_gpu"] = Enumerable.Range(0, _frames.Count).OrderByDescending(i => _frames[i].Gpu).Take(12).OrderBy(i => i)
                .Select(i => string.Create(CultureInfo.InvariantCulture, $"#{i} gpu {_frames[i].Gpu:0.0}"))
                .ToArray(),
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
