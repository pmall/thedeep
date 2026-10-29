using System;
using System.Collections.Generic;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// A machine in a room: one node type for every species (docs/long-term.md: never one scene per enemy).
/// On ready it builds its body from <see cref="Species"/> (parts, collision, brain, palette). Each physics
/// tick the <see cref="MachineDirector"/> hands it a <see cref="Perception"/> through <see cref="Step"/>:
/// its brain decides, its locomotion moves it (walking, hovering, or crawling over the metal block it
/// starts on), and its animator poses the body. Its origin is its feet (walkers, crawlers) or its center
/// (flyers).
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

    /// <summary>A hit stuns the brain this long, seconds (a wind-up in progress is lost).</summary>

    /// <summary>A broken machine folds down and sinks over this long, then is gone.</summary>
    private const float BreakTime = 0.6f;

    /// <summary>A crawler looks for its metal block this far around its origin, meters.</summary>
    private const float PerchSearch = 1.5f;

    /// <summary>A walker probes this far beyond its body for walls and drops, meters.</summary>
    private const float ProbeAhead = 0.35f;

    [Export] public MachineSpecies Species { get; set; } = null!;

    /// <summary>+1 facing right, -1 facing left, at spawn.</summary>
    [Export] public int StartFacing { get; set; } = 1;

    /// <summary>Varies this machine from others of its species (0: taken from its position).</summary>
    [Export] public int Seed { get; set; }

    private Node3D _frame = null!;
    private PartBody _body = null!;
    private MachineAnimator _animator = null!;
    private Vector3 _spawn;
    private SurfacePath? _path;
    private bool _pathSearched;
    private float _s;
    private float _crawlSpeed;
    private Vector2 _normal = new(0f, 1f);
    private float _roll;
    private float _yaw;
    private float _time;
    private float _walked;
    private float _stepping;
    private bool _blockedLeft, _blockedRight;
    private Vitality _vitality = null!;
    private float _stagger;
    private float _flash;
    private float _breaking = -1f;
    private Vector3 _frameScale = Vector3.One;

    public MachineBrain Brain { get; private set; } = null!;

    /// <summary>What the brain wanted at the last tick.</summary>
    public Intent Intent { get; private set; }

    public int Facing { get; private set; } = 1;

    /// <summary>Broken by the pilgrim: folding down, then gone until the room is entered again.</summary>
    public bool Broken => _breaking >= 0f;

    /// <summary>The box the body fills on the play plane (what a swing must reach).</summary>
    public Rect2 BodyRect
    {
        get
        {
            Vector2 up = _path is not null ? _normal : new Vector2(0f, 1f);
            Vector2 size = Species.BodySize;
            Vector2 center = PlanePosition + (Species.Locomotion == Locomotion.Hover ? Vector2.Zero : up * size.Y * 0.5f);
            Vector2 extent = Mathf.Abs(up.X) > 0.5f ? new Vector2(size.Y, size.X) : size;
            return new Rect2(center - extent * 0.5f, extent);
        }
    }

    /// <summary>Where the machine is on the play plane: its feet on its surface, or its center for flyers.</summary>
    public Vector2 PlanePosition => _path is not null ? Flat(_path.At(_s).Point) : new Vector2(GlobalPosition.X, GlobalPosition.Y);

    /// <summary>Where its line of sight starts.</summary>
    public Vector3 SensorPosition
    {
        get
        {
            Vector2 p = PlanePosition + (_path is not null ? _normal : new Vector2(0f, 1f)) * Species.SensorHeight;
            return new Vector3(p.X, p.Y, 0f);
        }
    }

    public override void _Ready()
    {
        CollisionLayer = PhysicsLayers.Enemy;
        CollisionMask = PhysicsLayers.Solid;
        AxisLockLinearZ = true;
        MotionMode = Species.Locomotion == Locomotion.Walk ? MotionModeEnum.Grounded : MotionModeEnum.Floating;
        float centerY = Species.Locomotion == Locomotion.Hover ? 0f : Species.BodySize.Y * 0.5f;
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Species.BodySize.X, Species.BodySize.Y, 0.6f) },
            Position = new Vector3(0f, centerY, 0f),
            // A crawler is placed along its surface, never pushed by physics.
            Disabled = Species.Locomotion == Locomotion.Crawl,
        });

        _frame = new Node3D { Name = "Frame" };
        _body = new PartBody { Name = "Body", Assembly = Species.Assembly };
        _frame.AddChild(_body);
        AddChild(_frame);
        _animator = new MachineAnimator(_body, _frame, Species);
        _spawn = GlobalPosition;
        if (Seed == 0)
        {
            Seed = 1 + (int)Mathf.PosMod(Mathf.Round(_spawn.X * 13f + _spawn.Y * 7f), 9973f);
        }

        _body.SetShaderParameter(WearParameter, Species.Wear);
        _body.SetShaderParameter(ToneParameter, Species.Tone);
        _body.SetShaderParameter(SeedParameter, Seed * 0.618034f % 1f);
        Reset();
    }

    /// <summary>Back where it spawned, with a fresh brain, at rest.</summary>
    public void Reset()
    {
        Brain = Species.Behavior.CreateBrain(Species, Seed);
        _vitality = new Vitality(Species.Health);
        _stagger = 0f;
        _flash = 0f;
        _breaking = -1f;
        Visible = true;
        _frame.Scale = Vector3.One;
        _body.SetShaderParameter(HurtParameter, 0f);
        GlobalPosition = _spawn;
        Velocity = Vector3.Zero;
        Facing = StartFacing < 0 ? -1 : 1;
        _yaw = TargetYaw;
        _time = 0f;
        _walked = 0f;
        _stepping = 0f;
        _crawlSpeed = 0f;
        Intent = default;
        if (_path is not null)
        {
            _s = _path.Closest(Flat(_spawn));
            _normal = Flat(_path.At(_s).Normal);
        }

        _roll = Mathf.Atan2(-_normal.X, _normal.Y);
        Place();
        _animator.Update(0f, 0f, Intent, 0f, 0f, 0f);
        _body.ResetChains();
        ResetPhysicsInterpolation();
    }

    /// <summary>Already broken (a boss beaten before): gone at once, no folding.</summary>
    public void Gone()
    {
        _breaking = BreakTime;
        _frameScale = _frame.Scale;
        Visible = false;
    }

    /// <summary>The perception's own part: time, place, facing, and what blocks the way.</summary>
    public Perception Perceive(PlayerTrace? player, IReadOnlyList<Impact> impacts, IReadOnlyList<Neighbor> neighbors) =>
        new(_time, ToNumerics(PlanePosition), Facing, player, impacts, neighbors, _blockedLeft, _blockedRight);

    /// <summary>
    /// A blow from <paramref name="from"/> (on the play plane): flashes, staggers, is pushed away, and breaks when
    /// its health is gone. Returns true if this blow broke it.
    /// </summary>
    public bool TakeHit(int damage, Vector2 from, float knockback)
    {
        if (Broken)
        {
            return false;
        }

        _vitality.Damage(damage);
        _flash = 1f;
        _stagger = Species.StaggerTime;
        float away = PlanePosition.X >= from.X ? 1f : -1f;
        if (Species.Locomotion != Locomotion.Crawl)
        {
            Velocity = new Vector3(away * knockback * Species.Heft, (Species.Locomotion == Locomotion.Hover ? 1f : 2f) * Species.Heft, 0f);
        }

        if (_vitality.Dead)
        {
            _breaking = 0f;
            _frameScale = _frame.Scale;
            return true;
        }

        return false;
    }

    /// <summary>One physics tick: think, move, animate.</summary>
    public void Step(float delta, in Perception perception)
    {
        if (Broken)
        {
            Fold(delta);
            return;
        }

        _flash = Mathf.Max(0f, _flash - delta * 5f);
        _body.SetShaderParameter(HurtParameter, _flash);
        if (Species.Locomotion == Locomotion.Crawl && !_pathSearched)
        {
            FindPerch();
        }

        _time += delta;
        Intent intent;
        if (_stagger > 0f)
        {
            // Reeling: the brain is stunned, the body slides to a stop from its knockback.
            _stagger -= delta;
            intent = default;
            intent.Rouse = Intent.Rouse;
        }
        else
        {
            intent = Brain.Think(perception, delta);
        }

        Intent = intent;
        Vector2 wanted = new(intent.Velocity.X, intent.Velocity.Y);
        float travelled = Species.Locomotion switch
        {
            // A crawler with nothing to cling to stays put.
            Locomotion.Crawl => _path is not null ? Crawl(wanted, delta) : 0f,
            Locomotion.Hover => Hover(wanted, delta),
            _ => Walk(wanted, delta),
        };

        // Facing: as asked, or the way it moves.
        int facing = intent.Facing != 0 ? intent.Facing : Mathf.Abs(travelled) > 1e-3f ? Math.Sign(travelled) * ForwardSign() : 0;
        if (facing != 0)
        {
            Facing = facing;
        }

        _yaw = Motion.Damp(_yaw, TargetYaw, Species.TurnSharpness, delta);
        _roll = Mathf.LerpAngle(_roll, Mathf.Atan2(-_normal.X, _normal.Y), 1f - Mathf.Exp(-14f * delta));
        Place();

        float speed = Mathf.Abs(travelled) / delta;
        _walked += Mathf.Abs(travelled);
        _stepping = Motion.Damp(_stepping, Mathf.Clamp(speed / 0.3f, 0f, 1f), 8f, delta);
        _animator.Update(delta, _time, intent, Mathf.Clamp(speed / Mathf.Max(Species.MaxSpeed, 0.1f), 0f, 1f), _walked, _stepping);
        _body.StepChains(delta, Species.Locomotion == Locomotion.Walk && IsOnFloor() ? GlobalPosition.Y + 0.02f : float.NaN);
        // Eyes take the hazard color only on machines that can hurt: color means gameplay.
        _body.SetShaderParameter(AlertParameter, Species.Hostile ? intent.Alert : 0f);
        _body.SetShaderParameter(RouseParameter, intent.Rouse);
        // A machine faces the way it moves, so its treads always roll forward.
        _body.SetShaderParameter(ScrollParameter, _walked);
    }

    /// <summary>Breaking apart: the body folds down and sinks, its glow dying, then the machine is gone.</summary>
    private void Fold(float delta)
    {
        _breaking += delta;
        float k = Mathf.Clamp(_breaking / BreakTime, 0f, 1f);
        float ease = k * k * (3f - 2f * k);
        _frame.Scale = new Vector3(_frameScale.X * (1f + 0.15f * ease), _frameScale.Y * (1f - 0.75f * ease), _frameScale.Z);
        _body.SetShaderParameter(HurtParameter, 1f - ease);
        _body.SetShaderParameter(RouseParameter, 1f - ease);
        if (k >= 1f)
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
        v.Y = IsOnFloor() ? -0.5f : Mathf.Max(v.Y - 25f * delta, -20f);
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
    private float Crawl(Vector2 wanted, float delta)
    {
        SurfacePath path = _path!;
        Vector2 tangent = Flat(path.Tangent(_s));
        float along = Mathf.Clamp(wanted.Dot(tangent), -Species.MaxSpeed, Species.MaxSpeed);
        _crawlSpeed = Motion.Approach(_crawlSpeed, along, Species.Acceleration * delta);
        float before = _s;
        _s = path.Wrap(_s + _crawlSpeed * delta);
        if (path.AtEnd(_s, Math.Sign(_crawlSpeed)))
        {
            _crawlSpeed = 0f;
        }

        float moved = _s - before;
        if (path.Closed && Mathf.Abs(moved) > path.Length * 0.5f)
        {
            moved -= Mathf.Sign(moved) * path.Length;
        }

        (System.Numerics.Vector2 point, System.Numerics.Vector2 normal) = path.At(_s);
        _normal = Flat(normal);
        GlobalPosition = new Vector3(point.X, point.Y, 0f);
        return moved;
    }

    /// <summary>+1 if moving along +X (or +s for a crawler) is moving forward for a machine facing right.</summary>
    private int ForwardSign()
    {
        if (_path is null)
        {
            return 1;
        }

        // A crawler's forward is its local +X turned by its roll; +s runs along the tangent.
        Vector2 forward = new Vector2(Mathf.Cos(_roll), Mathf.Sin(_roll));
        return forward.Dot(Flat(_path.Tangent(_s))) >= 0f ? 1 : -1;
    }

    private void Place()
    {
        Basis roll = new(Vector3.Back, _roll);
        _frame.Basis = roll * new Basis(Vector3.Up, _yaw);
    }

    /// <summary>A wall or a drop just beyond the body toward <paramref name="direction"/>.</summary>
    private bool Probe(int direction)
    {
        PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
        float edge = GlobalPosition.X + direction * (Species.BodySize.X * 0.5f + ProbeAhead);
        float knee = GlobalPosition.Y + Mathf.Min(0.4f, Species.BodySize.Y * 0.5f);
        var wall = PhysicsRayQueryParameters3D.Create(new Vector3(GlobalPosition.X, knee, 0f), new Vector3(edge, knee, 0f), PhysicsLayers.Solid);
        if (space.IntersectRay(wall).Count > 0)
        {
            return true;
        }

        var drop = PhysicsRayQueryParameters3D.Create(new Vector3(edge, knee, 0f), new Vector3(edge, GlobalPosition.Y - 0.6f, 0f), PhysicsLayers.Solid);
        return space.IntersectRay(drop).Count == 0;
    }

    /// <summary>A crawler's surface: the faces of the nearest metal block that nothing else touches.</summary>
    private void FindPerch()
    {
        _pathSearched = true;
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
        _path = SurfacePath.AroundBox(ToNumerics(min), ToNumerics(max), free, 0.01f);
        if (_path is not null)
        {
            _s = _path.Closest(Flat(_spawn));
            _normal = Flat(_path.At(_s).Normal);
            _roll = Mathf.Atan2(-_normal.X, _normal.Y);
            GlobalPosition = new Vector3(PlanePosition.X, PlanePosition.Y, 0f);
            Place();
            ResetPhysicsInterpolation();
        }
    }

    private bool FaceIsFree(Block perch, Vector3 center, Vector3 size)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = size },
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

    private static Vector2 Flat(System.Numerics.Vector2 v) => new(v.X, v.Y);

    private static NVector2 Flat(Vector3 v) => new(v.X, v.Y);

    private static NVector2 ToNumerics(Vector2 v) => new(v.X, v.Y);
}
