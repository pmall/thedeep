using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep.States;

/// <summary>
/// The Harpoon: the line flies out to an anchor while the pilgrim hangs, slowing; once it bites, the pilgrim is
/// pulled to it, gathering speed, until its center reaches the anchor, then lets go with part of that speed (and
/// a lift, so it rises over what the anchor hangs from) and the air dash back. Jump or dash during the pull
/// cancel into them, keeping momentum. A pull blocked by a solid lets go where it stands.
/// </summary>
public sealed class HarpoonState : PlayerState
{
    /// <summary>How fast the pilgrim's own motion dies while the line flies out (m/s²).</summary>
    private const float HangDamping = 60f;

    /// <summary>A pull moving less than this share of its speed, a few ticks running, is blocked.</summary>
    private const float StuckShare = 0.25f;

    private HarpoonAnchor? _anchor;
    private float _line;
    private float _speed;
    private bool _bitten;
    private bool _arriving;
    private int _stuck;
    private NVector2 _before;
    private NVector2 _pull;

    public HarpoonState(Player player) : base(player)
    {
    }

    public override bool AllowsTurning => false;

    public override MotionMode Motion => MotionModes.Harpoon(_bitten);

    /// <summary>The anchor fired at (null before the first shot).</summary>
    public HarpoonAnchor? Anchor => _anchor;

    /// <summary>How far the line has flown from the pilgrim's center (it stops at the anchor).</summary>
    public float Line => _line;

    /// <summary>Whether the line has reached the anchor and the pull has begun.</summary>
    public bool Bitten => _bitten;

    /// <summary>The anchor to fire at when entered: set by <see cref="Player.HarpoonFire"/> just before.</summary>
    public void Prepare(HarpoonAnchor anchor) => _anchor = anchor;

    public override void Enter()
    {
        _line = 0f;
        _speed = 0f;
        _bitten = false;
        _arriving = false;
        _stuck = 0;
        float dx = _anchor is { } anchor ? anchor.Point.X - Player.Center.X : 0f;
        if (Mathf.Abs(dx) > 0.1f)
        {
            Player.SetWorldFacing(dx > 0f ? 1 : -1);
        }
    }

    public override PlayerState? PhysicsUpdate(float delta)
    {
        if (!Player.Abilities.Has(Ability.Harpoon) || _anchor is not { } anchor || !GodotObject.IsInstanceValid(anchor))
        {
            return Player.States.Fall;
        }

        NVector2 to = anchor.Point - Player.Center;
        float distance = to.Length();
        NVector2 dir = distance > 1e-4f ? to / distance : new NVector2(0f, 1f);
        if (!_bitten)
        {
            _line += Tuning.HarpoonThrowSpeed * delta;
            Player.Velocity = Player.Velocity.MoveToward(Vector3.Zero, HangDamping * delta);
            if (_line < distance)
            {
                return null;
            }

            _line = distance;
            _bitten = true;
            NVector2 v = Player.Velocity.Flat();
            _speed = Mathf.Max(0f, NVector2.Dot(v, dir));
        }

        _line = distance;
        if (Player.JumpPressed)
        {
            return Player.States.Jump;
        }

        if (Player.WantsDash)
        {
            return Player.States.Dash;
        }

        _speed = TheDeep.Motion.Approach(_speed, Tuning.HarpoonZipSpeed, Tuning.HarpoonZipAcceleration * delta);
        _arriving = distance <= _speed * delta;
        NVector2 step = _arriving ? to / delta : dir * _speed;
        Player.Velocity = step.OnPlane();
        _before = Player.Center;
        _pull = dir;
        return null;
    }

    public override PlayerState? AfterMove()
    {
        if (!_bitten || _anchor is not { } anchor)
        {
            return null;
        }

        if (_arriving || (anchor.Point - Player.Center).Length() < 0.05f)
        {
            NVector2 v = HarpoonAim.Release(_pull, _speed, Tuning.HarpoonExitSpeedFactor, Tuning.HarpoonExitLift);
            Player.Velocity = v.OnPlane();
            Player.LastAnchor = anchor;
            Player.RefillAirDash();
            return Player.States.Fall;
        }

        float moved = (Player.Center - _before).Length();
        float expected = _speed / Engine.PhysicsTicksPerSecond;
        _stuck = moved < expected * StuckShare ? _stuck + 1 : 0;
        if (_stuck >= 4)
        {
            Player.LastAnchor = anchor;
            return Player.States.Fall;
        }

        return null;
    }
}
