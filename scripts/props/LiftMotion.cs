using System;

namespace TheDeep;

/// <summary>What a lift is doing (<see cref="LiftMotion"/>).</summary>
public enum LiftPhase
{
    /// <summary>At its lower stop, empty.</summary>
    Waiting,

    /// <summary>At its lower stop with a rider, about to let go of its brake.</summary>
    Boarding,

    /// <summary>On its way up.</summary>
    Rising,

    /// <summary>At its upper stop.</summary>
    Arrived,

    /// <summary>Empty, sinking back to its lower stop.</summary>
    Returning,
}

/// <summary>
/// The travel of a counterweighted lift that only carries its rider up, pure: standing on it at its lower stop lets go of
/// the brake after <see cref="BoardDelay"/>; then it gathers speed, rises at <see cref="Speed"/> and eases into its upper
/// stop <see cref="Rise"/> higher, braking so it arrives at rest. Once on its way it can't be stopped (a rider stepping off
/// doesn't hold it). Left empty at the top for <see cref="ReturnDelay"/>, it sinks back slowly to wait again; it never
/// carries a rider down. <see cref="Offset"/> is the height above the lower stop.
/// </summary>
public sealed class LiftMotion
{
    public LiftMotion(float rise, float speed, float acceleration, float boardDelay, float returnSpeed, float returnDelay)
    {
        Rise = rise;
        Speed = speed;
        Acceleration = acceleration;
        BoardDelay = boardDelay;
        ReturnSpeed = returnSpeed;
        ReturnDelay = returnDelay;
    }

    public float Rise { get; }
    public float Speed { get; }
    public float Acceleration { get; }
    public float BoardDelay { get; }
    public float ReturnSpeed { get; }
    public float ReturnDelay { get; }

    public LiftPhase Phase { get; private set; } = LiftPhase.Waiting;

    /// <summary>Height above the lower stop, meters (0 to <see cref="Rise"/>).</summary>
    public float Offset { get; private set; }

    /// <summary>Upward speed, m/s (negative while returning).</summary>
    public float Velocity { get; private set; }

    private float _timer;

    /// <summary>Advances by <paramref name="dt"/>; <paramref name="ridden"/> is whether someone stands on it. True on the tick the brake lets go.</summary>
    public bool Step(float dt, bool ridden)
    {
        switch (Phase)
        {
            case LiftPhase.Waiting:
                if (ridden)
                {
                    Phase = LiftPhase.Boarding;
                    _timer = 0f;
                }

                return false;

            case LiftPhase.Boarding:
                if (!ridden)
                {
                    Phase = LiftPhase.Waiting;
                    return false;
                }

                _timer += dt;
                if (_timer < BoardDelay)
                {
                    return false;
                }

                Phase = LiftPhase.Rising;
                return true;

            case LiftPhase.Rising:
                if (Travel(dt, Rise, Speed))
                {
                    Phase = LiftPhase.Arrived;
                    _timer = 0f;
                }

                return false;

            case LiftPhase.Arrived:
                _timer = ridden ? 0f : _timer + dt;
                if (_timer >= ReturnDelay)
                {
                    Phase = LiftPhase.Returning;
                }

                return false;

            default: // Returning
                if (Travel(dt, 0f, ReturnSpeed))
                {
                    Phase = LiftPhase.Waiting;
                }

                return false;
        }
    }

    /// <summary>Moves toward <paramref name="stop"/> at up to <paramref name="top"/> m/s, braking to arrive at rest; true once there.</summary>
    private bool Travel(float dt, float stop, float top)
    {
        float remaining = stop - Offset;
        float direction = MathF.Sign(remaining);
        float speed = MathF.Abs(Velocity);

        // The fastest it may go and still stop in time, then gathering speed toward the cruise.
        float brake = MathF.Sqrt(2f * Acceleration * MathF.Abs(remaining));
        speed = MathF.Min(Motion.Approach(speed, top, Acceleration * dt), brake);
        float step = MathF.Max(speed * dt, MathF.Min(Acceleration * dt * dt, MathF.Abs(remaining)));
        if (step >= MathF.Abs(remaining))
        {
            Offset = stop;
            Velocity = 0f;
            return true;
        }

        Offset += direction * step;
        Velocity = direction * speed;
        return false;
    }
}
