using System;

namespace TheDeep;

/// <summary>
/// The pilgrim's controls between the devices and its movement, pure: the input of each tick (a stick pushed far enough, a held
/// direction while steered, nothing while reeling from a blow), the presses kept a moment so an early one still counts (jump,
/// dash, harpoon), coyote time after walking off a ledge, the dash cooldown, the air dashes left, the pause before gripping again,
/// and the stun after a blow. The states ask it what the player wants; the player node feeds it the devices and the body's contacts.
/// </summary>
public sealed class PilgrimControl
{
    /// <summary>Stick deflection below which an axis counts as neutral.</summary>
    public const float StickThreshold = 0.3f;

    private readonly ControlLimits _limits;
    private readonly Countdown _coyote = new();
    private readonly Countdown _jumpBuffer = new();
    private readonly Countdown _dashBuffer = new();
    private readonly Countdown _dashCooldown = new();
    private readonly Countdown _regrab = new();
    private readonly Countdown _harpoonBuffer = new();
    private readonly Countdown _steer = new();
    private readonly Countdown _stun = new();
    private int _steerDirection;

    public PilgrimControl(ControlLimits limits)
    {
        _limits = limits;
        AirDashesLeft = limits.AirDashes;
    }

    /// <summary>The input of the last <see cref="Read"/>.</summary>
    public PlayerInput Input { get; private set; }

    public int AirDashesLeft { get; private set; }

    public float DashCooldownRemaining => _dashCooldown.Remaining;

    /// <summary>Reeling from a blow: the input counts for nothing.</summary>
    public bool Stunned => _stun.Active;

    /// <summary>Just let go of a surface: no grip for a moment, so the same one doesn't catch the body again.</summary>
    public bool GripHeldOff => _regrab.Active;

    /// <summary>A jump pressed a moment ago, and the ground (or coyote time) to jump from.</summary>
    public bool WantsJump => _jumpBuffer.Active && _coyote.Active;

    /// <summary>A jump pressed a moment ago, wherever the body is (leaping off a Mag-grip surface).</summary>
    public bool JumpPressed => _jumpBuffer.Active;

    /// <summary>A harpoon pressed a moment ago.</summary>
    public bool HarpoonPressed => _harpoonBuffer.Active;

    /// <summary>A dash pressed a moment ago, off cooldown, and an air dash left if <paramref name="onFloor"/> is false.</summary>
    public bool WantsDash(bool onFloor) => _dashBuffer.Active && !_dashCooldown.Active && (onFloor || AirDashesLeft > 0);

    public void Tick(float dt)
    {
        _coyote.Tick(dt);
        _jumpBuffer.Tick(dt);
        _dashBuffer.Tick(dt);
        _dashCooldown.Tick(dt);
        _regrab.Tick(dt);
        _harpoonBuffer.Tick(dt);
        _steer.Tick(dt);
        _stun.Tick(dt);
    }

    /// <summary>
    /// This tick's input from the devices. Steering ends once the body has <paramref name="landed"/> (on a floor, not rising). While
    /// stunned the input is nothing and presses are not kept.
    /// </summary>
    public PlayerInput Read(in RawInput raw, bool landed)
    {
        if (_steer.Active && landed)
        {
            _steer.Clear();
        }

        if (_stun.Active)
        {
            Input = default;
            return Input;
        }

        Input = new PlayerInput(MoveX: _steer.Active ? _steerDirection : Axis(raw.AxisX), MoveY: Axis(raw.AxisY), JumpHeld: raw.JumpHeld);
        if (raw.JumpPressed)
        {
            _jumpBuffer.Start(_limits.JumpBufferTime);
        }

        if (raw.DashPressed)
        {
            _dashBuffer.Start(_limits.DashBufferTime);
        }

        if (raw.HarpoonPressed)
        {
            _harpoonBuffer.Start(_limits.HarpoonBufferTime);
        }

        return Input;
    }

    /// <summary>Standing on a floor the normal way up: coyote time starts over and the air dashes come back.</summary>
    public void Stood()
    {
        _coyote.Start(_limits.CoyoteTime);
        RefillAirDash();
    }

    /// <summary>The air dashes back (a landing, an anchor reached, a Mag-grip surface, water).</summary>
    public void RefillAirDash() => AirDashesLeft = _limits.AirDashes;

    /// <summary>Taking off: the same press and ledge can't be used twice.</summary>
    public void ConsumeJump()
    {
        _jumpBuffer.Clear();
        _coyote.Clear();
    }

    public void ConsumeHarpoon() => _harpoonBuffer.Clear();

    /// <summary>A dash (or the Drill's dive) starts: the press is used, and off the floor an air dash is spent.</summary>
    public void BeginDash(bool onFloor)
    {
        _dashBuffer.Clear();
        if (!onFloor)
        {
            AirDashesLeft--;
        }
    }

    public void EndDash() => _dashCooldown.Start(_limits.DashCooldown);

    public void HoldOffGrip() => _regrab.Start(_limits.GripRegrabDelay);

    /// <summary>Holds the horizontal input toward <paramref name="direction"/> for up to <paramref name="seconds"/>, or until landing.</summary>
    public void Steer(int direction, float seconds)
    {
        _steerDirection = Math.Sign(direction);
        _steer.Start(seconds);
    }

    /// <summary>A blow landed: control is gone for a moment, and the presses kept are dropped.</summary>
    public void Hurt()
    {
        _stun.Start(_limits.HurtStun);
        _jumpBuffer.Clear();
        _dashBuffer.Clear();
    }

    /// <summary>Thrown back without harm (heat during the grace period): control is gone for a moment.</summary>
    public void Repelled() => _stun.Start(_limits.HurtStun);

    /// <summary>A fresh start (a respawn): nothing kept, nothing pending, the air dashes back.</summary>
    public void Reset()
    {
        _coyote.Clear();
        _jumpBuffer.Clear();
        _dashBuffer.Clear();
        _dashCooldown.Clear();
        _harpoonBuffer.Clear();
        _steer.Clear();
        _stun.Clear();
        RefillAirDash();
    }

    private static int Axis(float value) => MathF.Abs(value) < StickThreshold ? 0 : Math.Sign(value);
}
