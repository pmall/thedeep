using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep.States;

/// <summary>
/// Mag-grip: walking on a metal wall or ceiling, the body's up along the surface's normal. Held input
/// moves along the surface (up/down on a wall, left/right under a ceiling); pulling away lets go, jump
/// leaps off, dash lets go and dashes. Corners snap (<see cref="GripMath"/>): a metal wall ahead turns
/// the player onto it, the end of a surface wraps it around the edge; after a corner the held input keeps
/// meaning "keep going" until it changes. Reaching a floor stands the player up. Metal is the physics
/// layer only (world_metal), never the look.
/// </summary>
public sealed class GripState : PlayerState
{
    /// <summary>Speed pressing the body into the surface, so it stays in contact through small steps.</summary>
    private const float StickSpeed = 2f;

    /// <summary>How far ahead of the feet, past a surface's end, the edge is searched for.</summary>
    private const float EdgeSearch = 1f;

    private float _speed;
    private NVector2? _lockedInput;
    private float _lockedAlong;
    private GripPose _attach;

    public GripState(Player player) : base(player)
    {
    }

    public override bool AllowsTurning => false;

    public override MotionMode Motion => MotionMode.Ground;

    /// <summary>Where to attach when entered: set by <see cref="Player.GripAttach"/> just before.</summary>
    public void Prepare(GripPose pose) => _attach = pose;

    public override void Enter()
    {
        _speed = 0f;
        _lockedInput = null;
        Player.Orient(_attach);
        Player.Velocity = Vector3.Zero;
        NVector2 t = GripMath.Tangent(_attach.Up);
        NVector2 facingWay = _attach.Up.X != 0f ? new NVector2(0f, 1f) : new NVector2(Player.WorldFacing, 0f);
        Player.SetLocalFacing(NVector2.Dot(facingWay, t) < 0f ? -1 : 1);
    }

    public override void Exit()
    {
        Player.StandUpright();
    }

    public override PlayerState? PhysicsUpdate(float delta)
    {
        NVector2 up = Player.SurfaceUp;
        var input = new NVector2(Player.CurrentInput.MoveX, Player.CurrentInput.MoveY);

        if (!Player.Abilities.Has(Ability.MagGrip))
        {
            return LetGo();
        }

        if (Player.HarpoonFire() is { } harpoon)
        {
            return harpoon;
        }

        if (Player.JumpPressed)
        {
            return Leap(up);
        }

        if (Player.WantsDash)
        {
            LetGo();
            return Player.States.Dash;
        }

        // After a corner, the input that led there keeps meaning "keep going" (even if it now points away) until it changes.
        float along = GripMath.Along(input, up);
        if (_lockedInput is { } locked && locked == input)
        {
            along = _lockedAlong;
        }
        else
        {
            _lockedInput = null;
            if (GripMath.PullsAway(input, up))
            {
                return LetGo();
            }
        }

        float target = along * Tuning.GripSpeed;
        float rate = target != 0f && (_speed == 0f || Mathf.Sign(target) == Mathf.Sign(_speed)) ? Tuning.GripAcceleration : Tuning.GripDeceleration;
        _speed = TheDeep.Motion.Approach(_speed, target, rate * delta);
        if (along != 0f)
        {
            Player.SetLocalFacing(along > 0f ? 1 : -1);
        }

        NVector2 t = GripMath.Tangent(up);
        NVector2 v = t * _speed - up * StickSpeed;
        Player.Velocity = new Vector3(v.X, v.Y, 0f);
        return null;
    }

    public override PlayerState? AfterMove()
    {
        NVector2 up = Player.SurfaceUp;
        NVector2 travel = GripMath.Tangent(up) * Mathf.Sign(_speed);
        NVector2 feet = Player.Feet;

        // A wall ahead: metal turns the player onto it, anything else just stops it.
        if (_speed != 0f && Player.MetalContact(n => NVector2.Dot(n, travel) < -0.5f) is { } wall)
        {
            GripPose pose = GripMath.InnerCorner(feet, up, wall.Point, wall.Normal, Player.GripClearance);
            return Turn(pose, GripMath.TravelAfterInner(up));
        }

        if (Player.IsOnFloor())
        {
            return null;
        }

        // The surface ended under the feet: wrap around its edge if the end face is metal.
        if (_speed != 0f)
        {
            NVector2 from = feet - up * 0.1f;
            if (Player.MetalRay(from, from - travel * EdgeSearch) is { } edge)
            {
                GripPose pose = GripMath.OuterCorner(feet, up, travel, edge, Player.GripClearance);
                return Turn(pose, GripMath.TravelAfterOuter(up));
            }
        }

        return LetGo();
    }

    /// <summary>Snaps onto the next surface of a corner, still moving the same way along the perimeter.</summary>
    private PlayerState? Turn(GripPose pose, NVector2 travel)
    {
        float speed = Mathf.Abs(_speed);
        if (pose.Up.Y > 0.5f)
        {
            // Onto a floor: upright again, walking on.
            Player.Orient(pose);
            Player.Velocity = new Vector3(travel.X * speed, 0f, 0f);
            Player.SetWorldFacing(travel.X < 0f ? -1 : 1);
            return Player.CurrentInput.MoveX != 0 ? Player.States.Run : Player.States.Idle;
        }

        Player.Orient(pose);
        float sign = NVector2.Dot(travel, GripMath.Tangent(pose.Up)) < 0f ? -1f : 1f;
        _speed = speed * sign;
        _lockedInput = new NVector2(Player.CurrentInput.MoveX, Player.CurrentInput.MoveY);
        _lockedAlong = sign;
        Player.SetLocalFacing((int)sign);
        return null;
    }

    private PlayerState LetGo()
    {
        NVector2 up = Player.SurfaceUp;
        NVector2 along = GripMath.Tangent(up) * _speed;
        Player.StandUpright();
        Player.Velocity = new Vector3(along.X + up.X * 1.5f, along.Y + up.Y * 1.5f, 0f);
        Player.HoldOffGrip();
        return Player.States.Fall;
    }

    private PlayerState Leap(NVector2 up)
    {
        Player.ConsumeJump();
        Player.StandUpright();
        Player.HoldOffGrip();
        if (up.Y < -0.5f)
        {
            // Under a ceiling: push off downward.
            Player.Velocity = new Vector3(Player.CurrentInput.MoveX * Tuning.RunSpeed, -2f, 0f);
            return Player.States.Fall;
        }

        Player.Velocity = new Vector3(up.X * Tuning.GripLeapAwaySpeed, Tuning.JumpTakeOffSpeed * Tuning.GripLeapUpFactor, 0f);
        Player.SetWorldFacing(up.X < 0f ? -1 : 1);
        return Player.States.Fall;
    }
}
