namespace TheDeep.States;

/// <summary>
/// The Drill's dive (dash while holding down): straight down at DiveSpeed, boring through cracked rock, until the pilgrim
/// lands (or DiveMaxTime passes, then it falls). From the air it spends the air dash; from the ground only on cracked rock.
/// </summary>
public sealed class DiveState : PlayerState
{
    private float _timeLeft;
    private bool _firstTick;

    public DiveState(Player player) : base(player)
    {
    }

    public override bool AllowsTurning => false;

    public override bool Bores => true;

    public override MotionMode Motion => MotionMode.Air;

    public override void Enter()
    {
        _timeLeft = Tuning.DiveMaxTime;
        _firstTick = true;
        Player.BeginDive();
    }

    public override void Exit()
    {
        Player.EndDash();
    }

    public override PlayerState? PhysicsUpdate(float delta)
    {
        _timeLeft -= delta;
        Player.Velocity = new Godot.Vector3(0f, -Tuning.DiveSpeed, 0f);

        // IsOnFloor() reflects the previous move: the floor it started on is the one it drills through.
        bool landed = !_firstTick && Player.IsOnFloor();
        _firstTick = false;
        if (landed)
        {
            Player.Velocity = Godot.Vector3.Zero;
            return Player.CurrentInput.MoveX != 0 ? Player.States.Run : Player.States.Idle;
        }

        return _timeLeft > 0f ? null : Player.States.Fall;
    }
}
