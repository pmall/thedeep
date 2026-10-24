using System;
using Godot;
using TheDeep.States;

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
    private int _airDashesLeft;

    /// <summary>Raised after <see cref="Respawn"/> teleports the player.</summary>
    public event Action? Respawned;

    /// <summary>Raised on touching down, with the downward speed (m/s) just before.</summary>
    public event Action<float>? Landed;

    /// <summary>Raised when a dash starts, with its direction (+1 right, -1 left).</summary>
    public event Action<int>? Dashed;

    public AbilitySet Abilities { get; } = new();

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

    /// <summary>A buffered dash press, off cooldown, and an air dash left if airborne.</summary>
    public bool WantsDash => _dashBuffer.Active && !_dashCooldown.Active && (IsOnFloor() || _airDashesLeft > 0);

    public override void _Ready()
    {
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
        float dt = (float)delta;

        _coyote.Tick(dt);
        _jumpBuffer.Tick(dt);
        _dashBuffer.Tick(dt);
        _dashCooldown.Tick(dt);

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

        if (IsOnFloor())
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
            FloorSurface = null;
        }

        Visual.Animate(dt, Velocity, _state.Motion, IsOnFloor(), GlobalPosition.Y);
    }

    /// <summary>
    /// Teleports to <paramref name="position"/>, standing still and idle, turned toward
    /// <paramref name="facing"/> (+1 right, -1 left, 0 keeps the current facing).
    /// </summary>
    public void Respawn(Vector3 position, int facing = 0)
    {
        ChangeState(States.Idle);
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
        _airDashesLeft = Tuning.AirDashes;
        ResetPhysicsInterpolation();
        Visual.Snap();
        Respawned?.Invoke();
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
        CurrentInput = new PlayerInput(
            MoveX: Mathf.Abs(axisX) < StickThreshold ? 0 : Math.Sign(axisX),
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
