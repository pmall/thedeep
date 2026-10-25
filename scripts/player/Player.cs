using System;
using Godot;
using TheDeep.States;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// The machine pilgrim. A CharacterBody3D locked to the Z = 0 play plane, driven by a small
/// explicit state machine (see scripts/player/states). This class owns input sampling, the
/// shared timers (coyote, buffers, cooldown) and the movement helpers that states call.
/// </summary>
public partial class Player : CharacterBody3D
{
    /// <summary>Stick deflection below which horizontal input counts as neutral.</summary>
    private const float StickThreshold = 0.3f;

    [Export] public PlayerTuning Tuning { get; set; } = null!;
    [Export] public PlayerVisual Visual { get; set; } = null!;

    private PlayerState _state = null!;
    private readonly Countdown _coyote = new();
    private readonly Countdown _jumpBuffer = new();
    private readonly Countdown _dashBuffer = new();
    private readonly Countdown _dashCooldown = new();
    private readonly Countdown _regrab = new();
    private int _airDashesLeft;
    private readonly Countdown _steer = new();
    private int _steerDirection;
    private CapsuleShape3D _capsule = null!;
    private Vector3 _capsuleOffset;

    /// <summary>Raised after <see cref="Respawn"/> teleports the player.</summary>
    public event Action? Respawned;

    /// <summary>Raised after <see cref="Arrive"/> places the player in a new room.</summary>
    public event Action? Arrived;

    /// <summary>Raised on touching down, with the downward speed (m/s) just before.</summary>
    public event Action<float>? Landed;

    /// <summary>Raised when a dash starts, with its direction (+1 right, -1 left).</summary>
    public event Action<int>? Dashed;

    public AbilitySet Abilities { get; } = new();

    /// <summary>Held still, velocity kept, no input read (during a room transition's fade).</summary>
    public bool Frozen { get; set; }

    public PlayerStateSet States { get; private set; } = null!;

    public PlayerState CurrentState => _state;

    public PlayerInput CurrentInput { get; private set; }

    /// <summary>+1 facing right, -1 facing left.</summary>
    public int Facing { get; private set; } = 1;

    public float DashCooldownRemaining => _dashCooldown.Remaining;

    public int AirDashesLeft => _airDashesLeft;

    /// <summary>What the player stands on (from the floor's physics layer), or null in the air.</summary>
    public SurfaceKind? FloorSurface { get; private set; }

    /// <summary>A buffered jump press and the ground (or coyote time) to jump from.</summary>
    public bool WantsJump => _jumpBuffer.Active && _coyote.Active;

    /// <summary>A buffered jump press, wherever the player is (leaping off a Mag-grip surface).</summary>
    public bool JumpPressed => _jumpBuffer.Active;

    /// <summary>A buffered dash press, off cooldown, and an air dash left if airborne.</summary>
    public bool WantsDash => _dashBuffer.Active && !_dashCooldown.Active && (IsOnFloor() || _airDashesLeft > 0);

    /// <summary>The way the body's feet point away from (the surface's normal): world up, except on Mag-grip.</summary>
    public NVector2 SurfaceUp => new(UpDirection.X, UpDirection.Y);

    /// <summary>Whether the body stands the normal way up (not gripping a wall or ceiling).</summary>
    public bool IsUpright => UpDirection.Y > 0.5f;

    /// <summary>The feet (the body's origin) on the play plane.</summary>
    public NVector2 Feet => new(GlobalPosition.X, GlobalPosition.Y);

    /// <summary>Height of the body's center above the feet.</summary>
    public float CenterHeight => _capsuleOffset.Y;

    /// <summary>How far the feet stay from a surface the body lies along after a corner: the body's radius and a little air.</summary>
    public float GripClearance => _capsule.Radius + 0.02f;

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
        var collision = GetNode<CollisionShape3D>("Collision");
        _capsule = (CapsuleShape3D)collision.Shape;
        _capsuleOffset = collision.Position;
        CollisionLayer = PhysicsLayers.Player;
        CollisionMask = PhysicsLayers.Solid;
        States = new PlayerStateSet(this);
        _state = States.Idle;
        _state.Enter();
        _airDashesLeft = Tuning.AirDashes;
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

        _coyote.Tick(dt);
        _jumpBuffer.Tick(dt);
        _dashBuffer.Tick(dt);
        _dashCooldown.Tick(dt);
        _regrab.Tick(dt);
        _steer.Tick(dt);

        ReadInput();
        if (_state.AllowsTurning && CurrentInput.MoveX != 0)
        {
            SetFacing(CurrentInput.MoveX);
        }

        PlayerState? next = _state.PhysicsUpdate(dt);
        if (next is not null && next != _state)
        {
            ChangeState(next);
        }

        bool wasOnFloor = IsOnFloor();
        float fallSpeed = -Velocity.Y;
        MoveAndSlide();
        StayOnPlayPlane();
        if (_state.AfterMove() is { } after && after != _state)
        {
            ChangeState(after);
        }

        if (IsOnFloor() && IsUpright)
        {
            _coyote.Start(Tuning.CoyoteTime);
            _airDashesLeft = Tuning.AirDashes;
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
                _airDashesLeft = Tuning.AirDashes; // a Mag-grip surface refills the air dash like a floor
            }
        }

        // The animation sees the body's own frame: on a wall, walking up it is walking forward.
        Vector3 localVelocity = GlobalBasis.Inverse() * Velocity;
        Visual.Animate(dt, localVelocity, _state.Motion, IsOnFloor(), IsUpright ? GlobalPosition.Y : float.NaN);
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
        _coyote.Clear();
        _jumpBuffer.Clear();
        _dashBuffer.Clear();
        _dashCooldown.Clear();
        _steer.Clear();
        _airDashesLeft = Tuning.AirDashes;
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
        if (!Abilities.Has(Ability.MagGrip) || _regrab.Active || !IsUpright)
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

    /// <summary>Puts the body at <paramref name="pose"/> (feet and up), keeping the visible body where it was so it eases into place.</summary>
    public void Orient(GripPose pose)
    {
        Transform3D before = Visual.GlobalTransform;
        GlobalTransform = new Transform3D(new Basis(Vector3.Back, GripMath.Angle(pose.Up)), new Vector3(pose.Feet.X, pose.Feet.Y, 0f));
        UpDirection = new Vector3(pose.Up.X, pose.Up.Y, 0f);
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
    public void HoldOffGrip() => _regrab.Start(Tuning.GripRegrabDelay);

    /// <summary>Faces along the body's own forward (+1) or backward (-1).</summary>
    public void SetLocalFacing(int direction) => SetFacing(direction);

    /// <summary>Faces +X (+1) or -X (-1); only meaningful upright.</summary>
    public void SetWorldFacing(int direction) => SetFacing(direction);

    /// <summary>A contact from the last move with a metal surface whose (axis-snapped) normal passes <paramref name="accept"/>.</summary>
    public (NVector2 Normal, NVector2 Point)? MetalContact(Func<NVector2, bool> accept)
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            KinematicCollision3D collision = GetSlideCollision(i);
            Vector3 n = collision.GetNormal();
            NVector2 normal = GripMath.Snap(new NVector2(n.X, n.Y));
            if (collision.GetCollider() is CollisionObject3D body && (body.CollisionLayer & PhysicsLayers.WorldMetal) != 0 && accept(normal))
            {
                Vector3 p = collision.GetPosition();
                return (normal, new NVector2(p.X, p.Y));
            }
        }

        return null;
    }

    /// <summary>Where a ray on the play plane first hits metal, or null (rock, or nothing, in the way).</summary>
    public NVector2? MetalRay(NVector2 from, NVector2 to)
    {
        var query = PhysicsRayQueryParameters3D.Create(new Vector3(from.X, from.Y, 0f), new Vector3(to.X, to.Y, 0f), PhysicsLayers.Solid);
        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is not CollisionObject3D body || (body.CollisionLayer & PhysicsLayers.WorldMetal) == 0)
        {
            return null;
        }

        Vector3 p = hit["position"].AsVector3();
        return new NVector2(p.X, p.Y);
    }

    /// <summary>Whether the body fits at <paramref name="pose"/> (tested a little off the surface it stands on).</summary>
    private bool Fits(GripPose pose)
    {
        var basis = new Basis(Vector3.Back, GripMath.Angle(pose.Up));
        var origin = new Vector3(pose.Feet.X + pose.Up.X * 0.05f, pose.Feet.Y + pose.Up.Y * 0.05f, 0f);
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = _capsule,
            Transform = new Transform3D(basis, origin) * new Transform3D(Basis.Identity, _capsuleOffset),
            CollisionMask = PhysicsLayers.Solid,
            Margin = 0f,
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }

    /// <summary>
    /// Holds the horizontal input toward <paramref name="direction"/> for up to <paramref name="seconds"/>, or until
    /// the player lands (an upward arrival through a hatch, carried onto the floor beside it).
    /// </summary>
    public void Steer(int direction, float seconds)
    {
        _steerDirection = Math.Sign(direction);
        _steer.Start(seconds);
    }

    /// <summary>Accelerates horizontal velocity toward the held direction at run speed.</summary>
    public void ApplyRunControl(float delta, bool grounded)
    {
        float target = CurrentInput.MoveX * Tuning.RunSpeed;
        bool accelerating = target != 0f && (Velocity.X == 0f || Mathf.Sign(target) == Mathf.Sign(Velocity.X))
                            && Mathf.Abs(Velocity.X) <= Tuning.RunSpeed;
        float rate = (grounded, accelerating) switch
        {
            (true, true) => Tuning.GroundAcceleration,
            (true, false) => Tuning.GroundDeceleration,
            (false, true) => Tuning.AirAcceleration,
            (false, false) => Tuning.AirDeceleration,
        };
        Velocity = Velocity with { X = Motion.Approach(Velocity.X, target, rate * delta) };
    }

    /// <summary>
    /// Applies gravity: lighter near the apex while jump is held, heavier when falling, capped at MaxFallSpeed.
    /// </summary>
    public void ApplyGravity(float delta)
    {
        float gravity = Tuning.Gravity;
        float vy = Velocity.Y;
        if (CurrentInput.JumpHeld && Mathf.Abs(vy) < Tuning.ApexHangThreshold && !IsOnFloor())
        {
            gravity *= Tuning.ApexGravityMultiplier;
        }
        else if (vy < 0f)
        {
            gravity *= Tuning.FallGravityMultiplier;
        }

        Velocity = Velocity with { Y = Mathf.Max(vy - gravity * delta, -Tuning.MaxFallSpeed) };
    }

    /// <summary>Called by JumpState on take-off so the same press and ledge can't be used twice.</summary>
    public void ConsumeJump()
    {
        _jumpBuffer.Clear();
        _coyote.Clear();
    }

    public void BeginDash(int direction)
    {
        _dashBuffer.Clear();
        if (!IsOnFloor())
        {
            _airDashesLeft--;
        }

        SetFacing(direction);
        Dashed?.Invoke(direction);
    }

    public void EndDash() => _dashCooldown.Start(Tuning.DashCooldown);

    private void ReadInput()
    {
        float axisX = Input.GetAxis(InputActions.MoveLeft, InputActions.MoveRight);
        float axisY = Input.GetAxis(InputActions.MoveDown, InputActions.MoveUp);
        if (_steer.Active && IsOnFloor() && Velocity.Y <= 0f)
        {
            _steer.Clear();
        }

        CurrentInput = new PlayerInput(
            MoveX: _steer.Active ? _steerDirection : Mathf.Abs(axisX) < StickThreshold ? 0 : Math.Sign(axisX),
            MoveY: Mathf.Abs(axisY) < StickThreshold ? 0 : Math.Sign(axisY),
            JumpHeld: Input.IsActionPressed(InputActions.Jump));

        if (Input.IsActionJustPressed(InputActions.Jump))
        {
            _jumpBuffer.Start(Tuning.JumpBufferTime);
        }

        if (Input.IsActionJustPressed(InputActions.Dash))
        {
            _dashBuffer.Start(Tuning.DashBufferTime);
        }
    }

    private void ChangeState(PlayerState next)
    {
        _state.Exit();
        _state = next;
        _state.Enter();
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
            KinematicCollision3D collision = GetSlideCollision(i);
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
