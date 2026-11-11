namespace TheDeep.States;

/// <summary>
/// A fixed-distance horizontal burst, on the ground or in the air, with no gravity.
/// Ends after DashDuration or on hitting a wall; a grounded dash can be cancelled into a jump.
/// With the Drill it bores through cracked rock and carries on.
/// </summary>
public sealed class DashState : PlayerState
{
    /// <summary>How far ahead (seconds of the dash) cracked rock turns the dash into the bore, so the body is tucked when it hits.</summary>
    private const float BoreLookAhead = 0.08f;

    private float _timeLeft;
    private int _direction;
    private bool _firstTick;
    private bool _startedGrounded;
    private bool _boring;

    /// <summary>The dash state of <paramref name="player"/>, made once by <see cref="PlayerStateSet"/>.</summary>
    public DashState(Player player) : base(player)
    {
    }

    public override bool AllowsTurning => false;

    public override bool Bores => true;

    public override MotionMode Motion => MotionModes.Dash(_startedGrounded, _boring);

    public override void Enter()
    {
        _direction = Player.CurrentInput.MoveX != 0 ? Player.CurrentInput.MoveX : Player.Facing;
        _timeLeft = Tuning.DashDuration;
        _firstTick = true;
        _startedGrounded = Player.IsOnFloor();
        _boring = false;
        Player.BeginDash(_direction);
    }

    public override void Exit()
    {
        Player.EndDash();
    }

    public override PlayerState? PhysicsUpdate(float delta)
    {
        _timeLeft -= delta;
        float speed = Tuning.DashDistance / Tuning.DashDuration;
        Player.Velocity = new Godot.Vector3(_direction * speed, 0f, 0f);
        _boring = _boring || Player.FacesCrack(Player.Velocity * BoreLookAhead);

        if (Player.HarpoonFire() is { } harpoon)
        {
            return harpoon;
        }

        bool grounded = Player.IsOnFloor();
        if (grounded && Player.WantsJump)
        {
            return Player.States.Jump;
        }

        // IsOnWall() reflects the previous move, so ignore it on the first tick (the player may be
        // dashing away from a wall it was pressed against).
        bool hitWall = !_firstTick && Player.IsOnWall() && Player.GetWallNormal().X * _direction < -0.5f;
        _firstTick = false;

        if (_timeLeft > 0f && !hitWall)
        {
            return null;
        }

        Player.Velocity = new Godot.Vector3(_direction * Tuning.RunSpeed * Tuning.DashExitSpeedFactor, 0f, 0f);
        if (!grounded)
        {
            return Player.States.Fall;
        }

        return Player.CurrentInput.MoveX != 0 ? Player.States.Run : Player.States.Idle;
    }
}
