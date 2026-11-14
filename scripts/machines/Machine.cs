using System;
using System.Collections.Generic;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// A machine in a room: one node type for every species (docs/long-term.md#machines-distinct: never one scene per enemy).
/// On ready it builds its body from <see cref="Species"/> (parts, collision, brain, palette). Each physics
/// tick the <see cref="MachineDirector"/> hands it a <see cref="Perception"/> through <see cref="Step"/>:
/// its brain decides, its locomotion moves it (walking, hovering, or crawling over the metal block it
/// starts on), and its animator poses the body. Its origin is its feet (walkers, crawlers) or its center
/// (flyers). The rules are the core's: its condition (<see cref="MachineVitals"/>), its crawl (<see cref="CrawlMotion"/>) and its
/// gait (<see cref="MachineGait"/>); this node moves the body and the engine's pieces by them.
/// </summary>
public partial class Machine : CharacterBody3D
{
    // Instance shader parameters of assets/shaders/machine.gdshader.
    private static readonly StringName WearParameter = "wear";
    private static readonly StringName ToneParameter = "palette_tone";
    private static readonly StringName SeedParameter = "machine_seed";
    private static readonly StringName AlertParameter = "alert";
    private static readonly StringName RouseParameter = "rouse";
    private static readonly StringName ScrollParameter = "scroll";
    private static readonly StringName HurtParameter = "hurt";

    /// <summary>A crawler looks for its metal block this far around its origin, meters.</summary>
    private const float PerchSearch = 1.5f;

    /// <summary>A walker probes this far beyond its body for walls and drops, meters.</summary>
    private const float ProbeAhead = 0.35f;

    [Export] public MachineSpecies Species { get; set; } = null!;

    /// <summary>+1 facing right, -1 facing left, at spawn.</summary>
    [Export] public int StartFacing { get; set; } = 1;

    /// <summary>Varies this machine from others of its species (0: taken from its position).</summary>
    [Export] public int Seed { get; set; }

    private Node3D? _frame;
    private Node3D FrameNode => _frame ??= new Node3D() { Name = "Frame" };
    private PlaneRay? _ray;
    private PlaneRay Ray => _ray ??= PlaneRay.For(PhysicsLayers.Solid);
    private PartBody? _body;
    private PartBody BodyNode => _body ??= new PartBody() { Name = "Body" };
    private readonly MachineGait _gait = new();
    private MachineAnimator? _animator;
    private Vector3 _spawn;

    /// <summary>A crawler's way along its metal block, once found (null for walkers and flyers, or a crawler with no block).</summary>
    private CrawlMotion? _crawl;
    private bool _perchSearched;
    private Vector2 _normal = new(0f, 1f);
    private float _roll;
    private float _yaw;
    private float _time;
    private bool _blockedLeft, _blockedRight;
    private MachineVitals? _vitals;
    private MachineBrain? _brain;
    private Vector3 _frameScale = Vector3.One;

    private MachineAnimator Animator => _animator ??= new MachineAnimator(BodyNode, FrameNode, Species);

    private MachineVitals Vitals => _vitals ??= new MachineVitals(Species.Health, Species.StaggerTime);

    /// <summary>What decides what it does (fresh at every <see cref="Reset"/>).</summary>
    public MachineBrain Brain => _brain ??= Species.Behavior.CreateBrain(Species, Seed);

    /// <summary>What the brain wanted at the last tick.</summary>
    public Intent Intent { get; private set; }

    /// <summary>The side it faces: +1 right, -1 left.</summary>
    public int Facing { get; private set; } = 1;

    /// <summary>Broken by the pilgrim: folding down, then gone until the room is entered again.</summary>
    public bool Broken => Vitals.Broken;

    /// <summary>The box the body fills on the play plane (what a swing must reach).</summary>
    public Rect2 BodyRect
    {
        get
        {
            Vector2 up = _crawl is not null ? _normal : new Vector2(0f, 1f);
            Vector2 size = Species.BodySize;
            Vector2 center = PlanePosition + (Species.Locomotion == Locomotion.Hover ? Vector2.Zero : up * size.Y * 0.5f);
            Vector2 extent = Mathf.Abs(up.X) > 0.5f ? new Vector2(size.Y, size.X) : size;
            return new Rect2(center - extent * 0.5f, extent);
        }
    }

    /// <summary>Where the machine is on the play plane: its feet on its surface, or its center for flyers.</summary>
    public Vector2 PlanePosition => _crawl is not null ? _crawl.At.Point.ToGodot() : new Vector2(GlobalPosition.X, GlobalPosition.Y);

    /// <summary>Where its line of sight starts.</summary>
    public Vector3 SensorPosition
    {
        get
        {
            Vector2 p = PlanePosition + (_crawl is not null ? _normal : new Vector2(0f, 1f)) * Species.SensorHeight;
            return new Vector3(p.X, p.Y, 0f);
        }
    }

    public override void _Ready()
    {
        Ray.Attach(this);
        CollisionLayer = PhysicsLayers.Enemy;
        CollisionMask = PhysicsLayers.Solid;
        AxisLockLinearZ = true;
        MotionMode = Species.Locomotion == Locomotion.Walk ? MotionModeEnum.Grounded : MotionModeEnum.Floating;
        float centerY = Species.Locomotion == Locomotion.Hover ? 0f : Species.BodySize.Y * 0.5f;
        using var body = new BoxShape3D { Size = new Vector3(Species.BodySize.X, Species.BodySize.Y, 0.6f) };
        AddChild(new CollisionShape3D
        {
            Shape = body,
            Position = new Vector3(0f, centerY, 0f),
            // A crawler is placed along its surface, never pushed by physics.
            Disabled = Species.Locomotion == Locomotion.Crawl,
        });

        BodyNode.Assembly = Species.Assembly;
        FrameNode.AddChild(BodyNode);
        AddChild(FrameNode);
        _spawn = GlobalPosition;
        if (Seed == 0)
        {
            Seed = MachineGait.SeedAt(_spawn.X, _spawn.Y);
        }

        BodyNode.SetShaderParameter(WearParameter, Species.Wear);
        BodyNode.SetShaderParameter(ToneParameter, Species.Tone);
        BodyNode.SetShaderParameter(SeedParameter, Seed * 0.618034f % 1f);
        Reset();
    }

    /// <summary>Back where it spawned, with a fresh brain, at rest.</summary>
    public void Reset()
    {
        _brain = Species.Behavior.CreateBrain(Species, Seed);
        _vitals = new MachineVitals(Species.Health, Species.StaggerTime);
        Visible = true;
        FrameNode.Scale = Vector3.One;
        BodyNode.SetShaderParameter(HurtParameter, 0f);
        GlobalPosition = _spawn;
        Velocity = Vector3.Zero;
        Facing = StartFacing < 0 ? -1 : 1;
        _yaw = TargetYaw;
        _time = 0f;
        _gait.Reset();
        Intent = default;
        if (_crawl is not null)
        {
            _crawl.Reset(_spawn.Flat());
            _normal = _crawl.At.Normal.ToGodot();
        }

        _roll = Mathf.Atan2(-_normal.X, _normal.Y);
        Place();
        Animator.Update(0f, 0f, Intent, 0f, 0f, 0f);
        BodyNode.ResetChains();
        ResetPhysicsInterpolation();
    }

    private float _halt;

    /// <summary>Holds still for <paramref name="seconds"/> (a blow's hit stop), then goes on as it was.</summary>
    public void Halt(float seconds) => _halt = Mathf.Max(_halt, seconds);

    /// <summary>Already broken (a boss beaten before): gone at once, no folding.</summary>
    public void Gone()
    {
        Vitals.Vanish();
        _frameScale = FrameNode.Scale;
        Visible = false;
    }

    /// <summary>The perception's own part: time, place, facing, and what blocks the way.</summary>
    public Perception Perceive(PlayerTrace? player, IReadOnlyList<Impact> impacts, IReadOnlyList<Neighbor> neighbors) =>
        new(_time, PlanePosition.ToCore(), Facing, player, impacts, neighbors, _blockedLeft, _blockedRight);

    /// <summary>
    /// A blow from <paramref name="fromX"/> (on the play plane): flashes, staggers, is pushed away, and breaks when
    /// its health is gone. Returns true if this blow broke it.
    /// </summary>
    public bool TakeHit(int damage, float fromX, float knockback)
    {
        if (Broken)
        {
            return false;
        }

        bool broke = Vitals.Hit(damage);
        if (MachineVitals.Knockback(Species.Locomotion, PlanePosition.X, fromX, knockback, Species.Heft) is { } push)
        {
            Velocity = push.OnPlane();
        }

        if (broke)
        {
            _frameScale = FrameNode.Scale;
        }

        return broke;
    }

    /// <summary>One physics tick: think, move, animate.</summary>
    public void Step(float delta, in Perception perception)
    {
        if (Broken)
        {
            Fold(delta);
            return;
        }

        if (_halt > 0f)
        {
            _halt -= delta; // a blow's hit stop: held where it was struck, its knockback waiting
            Intent = Intent with { Hit = false }; // a blow lands on one tick, never again on each tick it is held
            return;
        }

        if (Species.Locomotion == Locomotion.Crawl && !_perchSearched)
        {
            FindPerch();
        }

        _time += delta;
        Intent intent = Vitals.Think(Brain, perception, Intent, delta);
        BodyNode.SetShaderParameter(HurtParameter, Vitals.Flash);
        Intent = intent;
        Vector2 wanted = intent.Velocity.ToGodot();
        float travelled = Species.Locomotion switch
        {
            // A crawler with nothing to cling to stays put.
            Locomotion.Crawl => _crawl is { } crawl ? Crawl(crawl, wanted, delta) : 0f,
            Locomotion.Hover => Hover(wanted, delta),
            _ => Walk(wanted, delta),
        };

        Facing = MachineGait.Facing(intent.Facing, travelled, ForwardSign(), Facing);
        _yaw = Motion.Damp(_yaw, TargetYaw, Species.TurnSharpness, delta);
        _roll = Mathf.LerpAngle(_roll, Mathf.Atan2(-_normal.X, _normal.Y), 1f - Mathf.Exp(-14f * delta));
        Place();

        float speed = _gait.Moved(travelled, delta);
        Animator.Update(delta, _time, intent, Mathf.Clamp(speed / Mathf.Max(Species.MaxSpeed, 0.1f), 0f, 1f), _gait.Walked, _gait.Stepping);
        BodyNode.StepChains(delta, Species.Locomotion == Locomotion.Walk && IsOnFloor() ? GlobalPosition.Y + 0.02f : float.NaN);
        // Eyes take the hazard color only on machines that can hurt: color means gameplay.
        BodyNode.SetShaderParameter(AlertParameter, Species.Hostile ? intent.Alert : 0f);
        BodyNode.SetShaderParameter(RouseParameter, intent.Rouse);
        // A machine faces the way it moves, so its treads always roll forward.
        BodyNode.SetShaderParameter(ScrollParameter, _gait.Walked);
    }

    /// <summary>Breaking apart: the body folds down and sinks, its glow dying, then the machine is gone.</summary>
    private void Fold(float delta)
    {
        Vitals.Fold(delta);
        float ease = Vitals.Folded;
        FrameNode.Scale = new Vector3(_frameScale.X * (1f + 0.15f * ease), _frameScale.Y * (1f - 0.75f * ease), _frameScale.Z);
        BodyNode.SetShaderParameter(HurtParameter, 1f - ease);
        BodyNode.SetShaderParameter(RouseParameter, 1f - ease);
        if (Vitals.Gone)
        {
            Visible = false;
        }
    }

    /// <summary>Walks along X under gravity; returns the distance moved this tick (signed, world X).</summary>
    private float Walk(Vector2 wanted, float delta)
    {
        float before = GlobalPosition.X;
        float vx = Mathf.Clamp(wanted.X, -Species.MaxSpeed, Species.MaxSpeed);
        Vector3 v = Velocity;
        v.X = Motion.Approach(v.X, vx, Species.Acceleration * delta);
        v.Y = MachineGait.Fall(v.Y, IsOnFloor(), delta);
        Velocity = v;
        MoveAndSlide();
        GlobalPosition = GlobalPosition with { Z = 0f };
        _blockedLeft = Probe(-1);
        _blockedRight = Probe(1);
        return GlobalPosition.X - before;
    }

    /// <summary>Flies toward the wanted velocity, no gravity; returns the distance moved along X.</summary>
    private float Hover(Vector2 wanted, float delta)
    {
        float before = GlobalPosition.X;
        if (wanted.Length() > Species.MaxSpeed)
        {
            wanted = wanted.Normalized() * Species.MaxSpeed;
        }

        Vector3 target = new(wanted.X, wanted.Y, 0f);
        Velocity = Velocity.MoveToward(target, Species.Acceleration * delta);
        MoveAndSlide();
        GlobalPosition = GlobalPosition with { Z = 0f };
        return GlobalPosition.X - before;
    }

    /// <summary>Crawls along its surface path by the wanted velocity's part along it; returns the distance moved along the path.</summary>
    private float Crawl(CrawlMotion crawl, Vector2 wanted, float delta)
    {
        float moved = crawl.Step(wanted.ToCore(), Species.MaxSpeed, Species.Acceleration, delta);
        (NVector2 point, NVector2 normal) = crawl.At;
        _normal = normal.ToGodot();
        GlobalPosition = point.OnPlane();
        return moved;
    }

    /// <summary>+1 if moving along +X (or +s for a crawler) is moving forward for a machine facing right.</summary>
    private int ForwardSign()
    {
        if (_crawl is null)
        {
            return 1;
        }

        // A crawler's forward is its local +X turned by its roll; +s runs along the tangent.
        Vector2 forward = new Vector2(Mathf.Cos(_roll), Mathf.Sin(_roll));
        return forward.Dot(_crawl.Tangent.ToGodot()) >= 0f ? 1 : -1;
    }

    private void Place()
    {
        Basis roll = new(Vector3.Back, _roll);
        FrameNode.Basis = roll * new Basis(Vector3.Up, _yaw);
    }

    /// <summary>A wall or a drop just beyond the body toward <paramref name="direction"/>.</summary>
    private bool Probe(int direction)
    {
        float edge = GlobalPosition.X + direction * (Species.BodySize.X * 0.5f + ProbeAhead);
        float knee = GlobalPosition.Y + Mathf.Min(0.4f, Species.BodySize.Y * 0.5f);
        if (Ray.Cast(new Vector3(GlobalPosition.X, knee, 0f), new Vector3(edge, knee, 0f)))
        {
            return true;
        }

        return !Ray.Cast(new Vector3(edge, knee, 0f), new Vector3(edge, GlobalPosition.Y - 0.6f, 0f));
    }

    /// <summary>A crawler's surface: the faces of the nearest metal block that nothing else touches.</summary>
    private void FindPerch()
    {
        _perchSearched = true;
        Block? perch = null;
        float best = PerchSearch;
        IEnumerable<Block> blocks = FindRoom()?.Blocks() ?? Array.Empty<Block>();
        foreach (Block block in blocks)
        {
            if (block.Surface != SurfaceKind.Metal)
            {
                continue;
            }

            Vector3 half = block.Size * 0.5f;
            Vector3 c = block.GlobalPosition;
            float dx = Mathf.Max(Mathf.Abs(_spawn.X - c.X) - half.X, 0f);
            float dy = Mathf.Max(Mathf.Abs(_spawn.Y - c.Y) - half.Y, 0f);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance <= best)
            {
                best = distance;
                perch = block;
            }
        }

        if (perch is null)
        {
            GD.PushWarning($"{Name}: a crawler with no metal block within {PerchSearch} m; it stays put");
            return;
        }

        Vector3 h = perch.Size * 0.5f;
        Vector2 min = new(perch.GlobalPosition.X - h.X, perch.GlobalPosition.Y - h.Y);
        Vector2 max = new(perch.GlobalPosition.X + h.X, perch.GlobalPosition.Y + h.Y);
        var free = new[]
        {
            FaceIsFree(perch, new Vector3((min.X + max.X) * 0.5f, min.Y - 0.1f, 0f), new Vector3(max.X - min.X - 0.2f, 0.1f, 0.5f)),
            FaceIsFree(perch, new Vector3(max.X + 0.1f, (min.Y + max.Y) * 0.5f, 0f), new Vector3(0.1f, max.Y - min.Y - 0.2f, 0.5f)),
            FaceIsFree(perch, new Vector3((min.X + max.X) * 0.5f, max.Y + 0.1f, 0f), new Vector3(max.X - min.X - 0.2f, 0.1f, 0.5f)),
            FaceIsFree(perch, new Vector3(min.X - 0.1f, (min.Y + max.Y) * 0.5f, 0f), new Vector3(0.1f, max.Y - min.Y - 0.2f, 0.5f)),
        };
        if (SurfacePath.AroundBox(min.ToCore(), max.ToCore(), free, 0.01f) is { } path)
        {
            _crawl = new CrawlMotion(path, _spawn.Flat());
            _normal = _crawl.At.Normal.ToGodot();
            _roll = Mathf.Atan2(-_normal.X, _normal.Y);
            GlobalPosition = new Vector3(PlanePosition.X, PlanePosition.Y, 0f);
            Place();
            ResetPhysicsInterpolation();
        }
    }

    private bool FaceIsFree(Block perch, Vector3 center, Vector3 size)
    {
        using var box = new BoxShape3D { Size = size };
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = box,
            Transform = new Transform3D(Basis.Identity, center),
            CollisionMask = PhysicsLayers.Solid,
            Exclude = new Godot.Collections.Array<Rid> { perch.GetRid() },
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }

    private Room? FindRoom()
    {
        Node? node = this;
        while (node is not null and not Room)
        {
            node = node.GetParent();
        }

        return node as Room;
    }

    /// <summary>Facing +X is yaw 0; facing -X is -π, so a turn passes through facing the camera.</summary>
    private float TargetYaw => Facing > 0 ? 0f : -Mathf.Pi;
}
