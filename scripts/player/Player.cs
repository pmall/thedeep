using System;
using Godot;
using TheDeep.States;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// The machine pilgrim. A CharacterBody3D locked to the Z = 0 play plane, driven by a small explicit state machine (see
/// scripts/player/states). The rules are the core's: its controls (<see cref="PilgrimControl"/>: input, the presses kept, coyote
/// time, cooldowns, air dashes, stun), how its speed changes (<see cref="MoveRules"/>), its health (<see cref="Vitality"/>). This
/// node feeds them the devices and the body's contacts, and moves the body by their answers.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public PlayerTuning Tuning { get; set; } = null!;
    [Export] public PlayerVisual Visual { get; set; } = null!;
    [Export] public PlayerAttack Attack { get; set; } = null!;

    /// <summary>The body's capsule (its shape is a <see cref="CapsuleShape3D"/>).</summary>
    [Export] public CollisionShape3D Collision { get; set; } = null!;

    private PlayerState? _state;
    private PilgrimControl? _control;
    private ControlLimits? _limits;
    private Vitality? _vitality;
    private PlayerStateSet? _states;
    private CapsuleShape3D? _capsule;
    private PlaneRay? _ray;
    private PlaneRay Ray => _ray ??= PlaneRay.For(PhysicsLayers.Solid);
    private ShapeProbe? _fit;
    private ShapeProbe Fit => _fit ??= ShapeProbe.For(PhysicsLayers.Solid);
    private readonly KinematicCollision3D _moveHit = new();

    // Built from the exports on first use (they are set before the node enters the tree).
    private PilgrimControl Control => _control ??= new PilgrimControl(Limits);
    private ControlLimits Limits => _limits ??= Tuning.Control;
    private CapsuleShape3D Capsule => _capsule ??= (CapsuleShape3D)Collision.Shape;

    /// <summary>Where the capsule sits over the feet (kept: read many times a tick, and each read crosses into the engine).</summary>
    private Vector3 CapsuleOffset => _capsuleOffset ??= Collision.Position;

    private Vector3? _capsuleOffset;

    /// <summary>Raised after <see cref="Respawn"/> teleports the player.</summary>
    public event Action? Respawned;

    /// <summary>Raised when a blow hurts the pilgrim (health already taken).</summary>
    public event Action? Hurt;

    /// <summary>Raised when the last health is lost.</summary>
    public event Action? Died;

    /// <summary>Raised after <see cref="Arrive"/> places the player in a new room.</summary>
    public event Action? Arrived;

    /// <summary>Raised on touching down, with the downward speed (m/s) just before.</summary>
    public event Action<float>? Landed;

    /// <summary>Raised when a dash starts, with its direction (+1 right, -1 left).</summary>
    public event Action<int>? Dashed;

    /// <summary>Raised when the Drill dives (straight down).</summary>
    public event Action? Dove;

    /// <summary>Raised when the Drill breaks cracked rock.</summary>
    public event Action<Block>? Bored;

    /// <summary>Raised on going into water deep enough to swim, with the downward speed (m/s) just before.</summary>
    public event Action<float>? Splashed;

    public AbilitySet Abilities { get; } = new();

    /// <summary>The items held (keys, codes): the doors they open (<see cref="LockedDoor"/>).</summary>
    public Inventory Items { get; } = new();

    /// <summary>The pilgrim's health (MaxHealth hits, a grace period after each).</summary>
    public Vitality Vitality => _vitality ??= new Vitality(Tuning.MaxHealth, Tuning.HurtGrace);

    /// <summary>Whether the player's input counts now (not frozen by a transition, not reeling from a hit).</summary>
    public bool InControl => !Frozen && !Control.Stunned && !Vitality.Dead;

    /// <summary>Held still, velocity kept, no input read (during a room transition's fade).</summary>
    public bool Frozen { get; set; }

    public PlayerStateSet States => _states ??= new PlayerStateSet(this);

    public PlayerState CurrentState => _state ??= States.Idle;

    public PlayerInput CurrentInput => Control.Input;

    /// <summary>+1 facing right, -1 facing left.</summary>
    public int Facing { get; private set; } = 1;

    public float DashCooldownRemaining => Control.DashCooldownRemaining;

    public int AirDashesLeft => Control.AirDashesLeft;

    /// <summary>What the player stands on (from the floor's physics layer), or null in the air.</summary>
    public SurfaceKind? FloorSurface { get; private set; }

    /// <summary>A buffered jump press and the ground (or coyote time) to jump from.</summary>
    public bool WantsJump => Control.WantsJump;

    /// <summary>A buffered jump press, wherever the player is (leaping off a Mag-grip surface).</summary>
    public bool JumpPressed => Control.JumpPressed;

    /// <summary>A buffered dash press, off cooldown, and an air dash left if airborne.</summary>
    public bool WantsDash => Control.WantsDash(IsOnFloor());

    /// <summary>The anchor the Harpoon would fire at now (in range and in sight), kept current by <see cref="HarpoonRig"/>.</summary>
    public HarpoonAnchor? AnchorInSight { get; set; }

    /// <summary>The anchor last zipped to: not a target again until the pilgrim stands or grips somewhere.</summary>
    public HarpoonAnchor? LastAnchor { get; set; }

    /// <summary>A buffered harpoon press, the ability, and an anchor in sight.</summary>
    public bool WantsHarpoon => Control.HarpoonPressed && AnchorInSight is not null && Abilities.Has(Ability.Harpoon);

    /// <summary>The height of the surface of the water the feet are in (a swimmable <see cref="WaterBody"/>), or null out of water.</summary>
    public float? WaterSurface { get; private set; }

    /// <summary>The height of the surface of the water the body's center is in, or null: what lets go of a Mag-grip under water.</summary>
    public float? CenterWater { get; private set; }

    /// <summary>Ballast's tanks are full: in water the body sinks instead of floating. Emptied whenever the pilgrim leaves the water.</summary>
    public bool Sunk { get; set; }

    /// <summary>The water numbers of <see cref="Tuning"/>, as <see cref="WaterMotion"/> reads them.</summary>
    public WaterLimits Water => _water ??= Tuning.Water;

    private WaterLimits? _water;

    /// <summary>The middle of the body on the play plane (what the harpoon aims from and pulls to an anchor).</summary>
    public NVector2 Center => Feet + SurfaceUp * CenterHeight;

    /// <summary>The way the body's feet point away from (the surface's normal): world up, except on Mag-grip.</summary>
    public NVector2 SurfaceUp => UpDirection.Flat();

    /// <summary>Whether the body stands the normal way up (not gripping a wall or ceiling).</summary>
    public bool IsUpright => UpDirection.Y > 0.5f;

    /// <summary>The feet (the body's origin) on the play plane.</summary>
    public NVector2 Feet => GlobalPosition.Flat();

    /// <summary>The body on the play plane (upright: the capsule's width and height over the feet).</summary>
    public Rect2 BodyRect => new(GlobalPosition.X - Capsule.Radius, GlobalPosition.Y, 2f * Capsule.Radius, 2f * CenterHeight);

    /// <summary>Height of the body's center above the feet.</summary>
    public float CenterHeight => CapsuleOffset.Y;

    /// <summary>How far the feet stay from a surface the body lies along after a corner: the body's radius and a little air.</summary>
    public float GripClearance => Capsule.Radius + 0.02f;

    /// <summary>The facing as the world sees it: +1 toward +X, -1 toward -X (on a wall, the way the head points along X, else the local facing).</summary>
    public int WorldFacing
    {
        get
        {
            float x = GripMath.Tangent(SurfaceUp).X * Facing;
            return x > 0.1f ? 1 : x < -0.1f ? -1 : Facing;
        }
    }

    public override void _Ready()
    {
        CollisionLayer = PhysicsLayers.Player;
        CollisionMask = PhysicsLayers.Solid;
        Ray.Attach(this);
        Fit.Attach(this, Capsule);
        CurrentState.Enter();
        Visual.Setup(Tuning.RunSpeed);
        Visual.Snap();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Frozen)
        {
            return;
        }

        float dt = (float)delta;

        Control.Tick(dt);
        Vitality.Tick(dt);
        Visual.Blink(Vitality.Grace > 0f && !Vitality.Dead);

        ReadInput();
        WaterSurface = WaterAt(GlobalPosition + Vector3.Up * 0.02f);
        CenterWater = IsUpright ? WaterSurface : WaterAt(Center.OnPlane());
        if (WaterSurface is null)
        {
            Sunk = false;
        }

        if (CurrentState.AllowsTurning && CurrentInput.MoveX != 0)
        {
            SetFacing(CurrentInput.MoveX);
        }

        PlayerState? next = CurrentState.PhysicsUpdate(dt);
        if (next is not null && next != CurrentState)
        {
            ChangeState(next);
        }

        bool wasOnFloor = IsOnFloor();
        float fallSpeed = -Velocity.Y;
        if (CurrentState.Bores)
        {
            Bore(Velocity * dt * 2f);
        }

        PushSpeed = Velocity.X;
        MoveAndSlide();
        StayOnPlayPlane();
        if (CurrentState.AfterMove() is { } after && after != CurrentState)
        {
            ChangeState(after);
        }

        if (IsOnFloor())
        {
            LastAnchor = null;
        }

        if (IsOnFloor() && IsUpright)
        {
            Control.Stood();
            FloorSurface = FloorSurfaceKind();
            if (!wasOnFloor)
            {
                Visual.Land(fallSpeed);
                Landed?.Invoke(fallSpeed);
            }
        }
        else
        {
            FloorSurface = IsOnFloor() ? SurfaceKind.Metal : null;
            if (FloorSurface is not null)
            {
                Control.RefillAirDash(); // a Mag-grip surface refills the air dash like a floor
            }
        }

        // The animation sees the body's own frame: on a wall, walking up it is walking forward.
        Vector3 localVelocity = GlobalBasis.Inverse() * Velocity;
        Visual.Animate(dt, localVelocity, CurrentState.Motion, IsOnFloor(), IsUpright ? GlobalPosition.Y : float.NaN);
    }

    /// <summary>
    /// Teleports to <paramref name="position"/>, standing still and idle, turned toward
    /// <paramref name="facing"/> (+1 right, -1 left, 0 keeps the current facing).
    /// </summary>
    public void Respawn(Vector3 position, int facing = 0)
    {
        ChangeState(States.Idle);
        StandUpright();
        if (facing != 0)
        {
            SetFacing(facing);
        }

        GlobalPosition = position with { Z = 0f };
        Velocity = Vector3.Zero;
        Control.Reset();
        LastAnchor = null;
        Sunk = false;
        ResetPhysicsInterpolation();
        Visual.Snap();
        Respawned?.Invoke();
    }

    /// <summary>
    /// Places the player in a newly loaded room, keeping its state, velocity and momentum (crossing a
    /// doorway at a run keeps running).
    /// </summary>
    public void Arrive(Vector3 position, Vector3 velocity)
    {
        if (!IsUpright)
        {
            ChangeState(States.Fall);
        }

        GlobalPosition = position with { Z = 0f };
        Velocity = velocity with { Z = 0f };
        ResetPhysicsInterpolation();
        Arrived?.Invoke();
    }

    /// <summary>
    /// Mag-grip, if it applies now: rising into a metal ceiling, or pressing into a metal wall. Returns the
    /// grip state, prepared to attach, or null. Called by the upright states before they move.
    /// </summary>
    public GripState? GripAttach()
    {
        if (!Abilities.Has(Ability.MagGrip) || Control.GripHeldOff || !IsUpright)
        {
            return null;
        }

        GripPose? pose = null;
        if (IsOnCeiling() && Velocity.Y >= 0f && MetalContact(n => n.Y < -0.5f) is { } ceiling)
        {
            pose = GripMath.OntoCeiling(Feet, ceiling.Point.Y);
        }
        else if (IsOnWall() && CurrentInput.MoveX != 0 && MetalContact(n => n.X * CurrentInput.MoveX < -0.5f) is { } wall)
        {
            pose = GripMath.OntoWall(Feet, CenterHeight, wall.Point.X, wall.Normal);
        }

        if (pose is not { } p || !Fits(p))
        {
            return null;
        }

        States.Grip.Prepare(p);
        return States.Grip;
    }

    /// <summary>
    /// The Harpoon, if it fires now (pressed, an anchor in sight): returns the harpoon state, aimed, or null. Called by every
    /// state the pilgrim can fire from, before anything else.
    /// </summary>
    public HarpoonState? HarpoonFire()
    {
        if (!Control.HarpoonPressed || AnchorInSight is not { } anchor || !Abilities.Has(Ability.Harpoon))
        {
            return null;
        }

        Control.ConsumeHarpoon();
        States.Harpoon.Prepare(anchor);
        return States.Harpoon;
    }

    /// <summary>
    /// Swimming, if it applies now: the feet deep enough in water and not rising out of it. Returns the swim state or null.
    /// Called by the states that can walk, fall or dash into water, before they move.
    /// </summary>
    public SwimState? SwimEnter() =>
        WaterSurface is { } surface && IsUpright && Velocity.Y <= 0f && WaterMotion.Deep(Feet.Y, surface, Water) ? States.Swim : null;

    /// <summary>Called by SwimState as it starts: the plunge is caught by the surface, and the air dash is back.</summary>
    public void BeginSwim()
    {
        float plunge = -Velocity.Y;
        Velocity = Velocity with { Y = WaterMotion.Splash(Velocity.Y, Water) };
        Control.RefillAirDash();
        Splashed?.Invoke(plunge);
    }

    /// <summary>An anchor reached: the air dash is back, like on a landing.</summary>
    public void RefillAirDash() => Control.RefillAirDash();

    /// <summary>Puts the body at <paramref name="pose"/> (feet and up), keeping the visible body where it was so it eases into place.</summary>
    public void Orient(GripPose pose)
    {
        Transform3D before = Visual.GlobalTransform;
        GlobalTransform = new Transform3D(new Basis(Vector3.Back, GripMath.Angle(pose.Up)), pose.Feet.OnPlane());
        UpDirection = pose.Up.OnPlane();
        ResetPhysicsInterpolation();
        Visual.CarryOver(before);
    }

    /// <summary>Back to the normal way up after Mag-grip, turned about the body's center; facing kept as the world sees it.</summary>
    public void StandUpright()
    {
        if (IsUpright && GlobalBasis.IsEqualApprox(Basis.Identity))
        {
            return;
        }

        int facing = WorldFacing;
        Orient(new GripPose(GripMath.Upright(Feet, SurfaceUp, CenterHeight), new NVector2(0f, 1f)));
        SetFacing(facing);
    }

    /// <summary>No grip for a moment (after letting go or leaping off), so the same surface doesn't catch the player again.</summary>
    public void HoldOffGrip() => Control.HoldOffGrip();

    /// <summary>Faces along the body's own forward (+1) or backward (-1).</summary>
    public void SetLocalFacing(int direction) => SetFacing(direction);

    /// <summary>Faces +X (+1) or -X (-1); only meaningful upright.</summary>
    public void SetWorldFacing(int direction) => SetFacing(direction);

    /// <summary>A contact from the last move with a metal surface whose (axis-snapped) normal passes <paramref name="accept"/>.</summary>
    public (NVector2 Normal, NVector2 Point)? MetalContact(Func<NVector2, bool> accept)
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            using KinematicCollision3D collision = GetSlideCollision(i); // disposed: a live wrapper makes the engine allocate a new one next call
            NVector2 normal = GripMath.Snap(collision.GetNormal().Flat());
            if (collision.GetCollider() is CollisionObject3D body && (body.CollisionLayer & PhysicsLayers.WorldMetal) != 0 && accept(normal))
            {
                return (normal, collision.GetPosition().Flat());
            }
        }

        return null;
    }

    /// <summary>The horizontal speed the body had going into the last move (before a wall stopped it).</summary>
    public float PushSpeed { get; private set; }

    /// <summary>
    /// Ledge assist (<see cref="TheDeep.LedgeAssist"/>), after an airborne move: pressing into a wall whose top is at most
    /// <see cref="PlayerTuning.LedgeAssistHeight"/> above the feet lifts the pilgrim onto it, just past the edge, keeping its way and
    /// speed; the visible body eases up from where it was. Returns whether it did. Called by the jump and fall states.
    /// </summary>
    public bool LedgeAssist()
    {
        int dir = CurrentInput.MoveX;
        if (!IsUpright || dir == 0 || !IsOnWall() || GetWallNormal().X * dir > -0.5f || Mathf.Sign(PushSpeed) != dir)
        {
            return false;
        }

        // The top of the wall, looked for from above the reach, just past its face.
        float probeX = GlobalPosition.X + dir * (Capsule.Radius + TheDeep.LedgeAssist.Reach);
        float assist = Tuning.LedgeAssistHeight;
        if (!Ray.Cast(new Vector3(probeX, GlobalPosition.Y + assist + 0.05f, 0f), new Vector3(probeX, GlobalPosition.Y, 0f))
            || Ray.GetCollisionNormal().Y < 0.5f)
        {
            return false;
        }

        float top = Ray.GetCollisionPoint().Y;
        if (top - GlobalPosition.Y > assist)
        {
            return false;
        }

        var feet = new NVector2(GlobalPosition.X + dir * TheDeep.LedgeAssist.Reach, top + 0.01f);
        if (!Fits(new GripPose(feet, new NVector2(0f, 1f))))
        {
            return false;
        }

        Orient(new GripPose(feet, new NVector2(0f, 1f)));
        Velocity = new Vector3(dir * TheDeep.LedgeAssist.Speed(PushSpeed, Tuning.RunSpeed), 0f, 0f);
        return true;
    }

    /// <summary>What a ray on the play plane first hits: metal, rock, or null (nothing).</summary>
    public SurfaceKind? SurfaceRay(NVector2 from, NVector2 to)
    {
        if (!Ray.Cast(from.OnPlane(), to.OnPlane()) || Ray.Body is not { } body)
        {
            return null;
        }

        return (body.CollisionLayer & PhysicsLayers.WorldMetal) != 0 ? SurfaceKind.Metal : SurfaceKind.Rock;
    }

    /// <summary>Where a ray on the play plane first hits metal, or null (rock, or nothing, in the way).</summary>
    public NVector2? MetalRay(NVector2 from, NVector2 to)
    {
        if (!Ray.Cast(from.OnPlane(), to.OnPlane()) || Ray.Body is not { } body
            || (body.CollisionLayer & PhysicsLayers.WorldMetal) == 0)
        {
            return null;
        }

        return Ray.GetCollisionPoint().Flat();
    }

    /// <summary>Whether the body fits at <paramref name="pose"/> (tested a little off the surface it stands on).</summary>
    private bool Fits(GripPose pose)
    {
        var basis = new Basis(Vector3.Back, GripMath.Angle(pose.Up));
        var origin = new Vector3(pose.Feet.X + pose.Up.X * 0.05f, pose.Feet.Y + pose.Up.Y * 0.05f, 0f);
        return !Fit.Overlaps(new Transform3D(basis, origin) * new Transform3D(Basis.Identity, CapsuleOffset));
    }

    /// <summary>
    /// A blow from <paramref name="from"/> (on the play plane): takes <paramref name="damage"/> health unless in the
    /// grace period, throws the pilgrim back and up, and briefly takes control away. Returns whether it hurt.
    /// </summary>
    public bool TakeHit(int damage, NVector2 from)
    {
        if (!Vitality.Damage(damage))
        {
            return false;
        }

        ChangeState(States.Fall);
        Velocity = MoveRules.Knockback(GlobalPosition.X, from.X, Limits).OnPlane();
        Control.Hurt();
        Hurt?.Invoke();
        if (Vitality.Dead)
        {
            Died?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Thrown back from <paramref name="from"/> (on the play plane) like a blow, without the damage: heat pushing the pilgrim
    /// out while its grace period keeps blows from hurting.
    /// </summary>
    public void Repel(NVector2 from)
    {
        if (CurrentState != States.Fall)
        {
            ChangeState(States.Fall);
        }

        Velocity = MoveRules.Knockback(GlobalPosition.X, from.X, Limits).OnPlane();
        Control.Repelled();
    }

    /// <summary>
    /// Holds the horizontal input toward <paramref name="direction"/> for up to <paramref name="seconds"/>, or until
    /// the player lands (an upward arrival through a hatch, carried onto the floor beside it).
    /// </summary>
    public void Steer(int direction, float seconds) => Control.Steer(direction, seconds);

    /// <summary>Accelerates horizontal velocity toward the held direction at run speed.</summary>
    public void ApplyRunControl(float delta, bool grounded) =>
        Velocity = Velocity with { X = MoveRules.Run(Velocity.X, CurrentInput.MoveX, grounded, Limits, delta) };

    /// <summary>Applies gravity: lighter near the apex while jump is held, heavier when falling, capped at MaxFallSpeed.</summary>
    public void ApplyGravity(float delta) =>
        Velocity = Velocity with { Y = MoveRules.Fall(Velocity.Y, CurrentInput.JumpHeld, IsOnFloor(), Limits, delta) };

    /// <summary>Called by JumpState on take-off so the same press and ledge can't be used twice.</summary>
    public void ConsumeJump() => Control.ConsumeJump();

    public void BeginDash(int direction)
    {
        Control.BeginDash(IsOnFloor());
        SetFacing(direction);
        Dashed?.Invoke(direction);
    }

    public void EndDash() => Control.EndDash();

    /// <summary>
    /// The dash a press starts: with the Drill and down held, the dive (from the air, or standing on cracked rock; in water
    /// only with Ballast's tanks full); otherwise the dash.
    /// </summary>
    public PlayerState DashMove() =>
        Abilities.Has(Ability.Drill) && CurrentInput.MoveY < 0 && IsUpright && (WaterSurface is null || Sunk)
        && (!IsOnFloor() || CrackedAt(Vector3.Down * 0.1f) is not null)
            ? States.Dive
            : States.Dash;

    /// <summary>Called by DiveState as it starts: like a dash, it spends the air dash off the ground.</summary>
    public void BeginDive()
    {
        Control.BeginDash(IsOnFloor());
        Dove?.Invoke();
    }

    /// <summary>With the Drill, breaks the cracked rock the body would run into moving by <paramref name="motion"/>.</summary>
    private void Bore(Vector3 motion)
    {
        if (!Abilities.Has(Ability.Drill) || motion.LengthSquared() < 1e-6f)
        {
            return;
        }

        // A few in a row: breaking one may show another right behind it.
        for (int i = 0; i < 3 && CrackedAt(motion) is { } block; i++)
        {
            block.Break(motion.Normalized());
            Bored?.Invoke(block);
        }
    }

    /// <summary>The surface height of the swimmable water at <paramref name="point"/>, or null.</summary>
    private static float? WaterAt(Vector3 point) => WaterBody.SurfaceAt(point.X, point.Y);

    /// <summary>The unbroken cracked rock the body would hit first moving by <paramref name="motion"/>, or null.</summary>
    private Block? CrackedAt(Vector3 motion)
    {
        return TestMove(GlobalTransform, motion, _moveHit) && _moveHit.GetCollider() is Block { Cracked: true, Broken: false } block ? block : null;
    }

    private void ReadInput()
    {
        var raw = new RawInput(
            AxisX: Input.GetAxis(InputActions.MoveLeft, InputActions.MoveRight),
            AxisY: Input.GetAxis(InputActions.MoveDown, InputActions.MoveUp),
            JumpHeld: Input.IsActionPressed(InputActions.Jump),
            JumpPressed: Input.IsActionJustPressed(InputActions.Jump),
            DashPressed: Input.IsActionJustPressed(InputActions.Dash),
            HarpoonPressed: Input.IsActionJustPressed(InputActions.Harpoon));
        Control.Read(raw, landed: IsOnFloor() && Velocity.Y <= 0f);
    }

    private void ChangeState(PlayerState next)
    {
        CurrentState.Exit();
        _state = next;
        next.Enter();
    }

    private void SetFacing(int direction)
    {
        Facing = direction < 0 ? -1 : 1;
        Visual.SetFacing(Facing);
    }

    /// <summary>The surface under the player, from the physics layer of the floor it touched (metal wins).</summary>
    private SurfaceKind FloorSurfaceKind()
    {
        SurfaceKind kind = SurfaceKind.Rock;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            using KinematicCollision3D collision = GetSlideCollision(i); // disposed: a live wrapper makes the engine allocate a new one next call
            if (collision.GetNormal().Y > 0.7f && collision.GetCollider() is CollisionObject3D body
                && (body.CollisionLayer & PhysicsLayers.WorldMetal) != 0)
            {
                kind = SurfaceKind.Metal;
            }
        }

        return kind;
    }

    /// <summary>All gameplay happens on X/Y; AxisLockLinearZ covers physics, this covers everything else.</summary>
    private void StayOnPlayPlane()
    {
        if (Position.Z != 0f)
        {
            Position = Position with { Z = 0f };
        }

        if (Velocity.Z != 0f)
        {
            Velocity = Velocity with { Z = 0f };
        }
    }
}
