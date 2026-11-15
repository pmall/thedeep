using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The camera's shake, pure: a trauma from 0 to 1 that fades, and an offset that grows with its square (small amounts stay
/// subtle), mostly vertical, from a few incommensurate sines: a jolt, never a rattle.
/// </summary>
public sealed class CameraShake
{
    /// <summary>The trauma the hardest landing adds.</summary>
    public const float LandingTrauma = 0.75f;

    /// <summary>The weakest share of <see cref="LandingTrauma"/> a landing above the threshold adds.</summary>
    public const float LandingFloor = 0.25f;

    private const float SideShare = 0.5f;
    private const float SideRate = 37f;
    private const float SideSlow = 13f;
    private const float UpRate = 43f;
    private const float UpPhase = 1.3f;
    private const float UpSlow = 17f;

    private float _trauma;
    private float _time;

    /// <summary>How shaken the camera is now, 0 to 1.</summary>
    public float Trauma => _trauma;

    /// <summary>Adds shake (0 to 1), up to the full.</summary>
    public void Add(float amount) => _trauma = Math.Clamp(_trauma + amount, 0f, 1f);

    /// <summary>Adds the shake of a landing at <paramref name="impactSpeed"/> m/s: none up to <paramref name="threshold"/>, then growing to the full at <paramref name="hardest"/>.</summary>
    public void AddLanding(float impactSpeed, float threshold, float hardest)
    {
        if (impactSpeed > threshold)
        {
            Add(LandingTrauma * Math.Clamp((impactSpeed - threshold) / (hardest - threshold), LandingFloor, 1f));
        }
    }

    /// <summary>Fades the trauma by <paramref name="decay"/> per second over <paramref name="dt"/> seconds and moves the shake on.</summary>
    public void Tick(float dt, float decay)
    {
        _trauma = MathF.Max(0f, _trauma - decay * dt);
        _time += dt;
    }

    /// <summary>The offset (meters) the view is shaken by now, <paramref name="amplitude"/> at the largest.</summary>
    public Vector2 Offset(float amplitude)
    {
        float shake = _trauma * _trauma * amplitude;
        return new Vector2(
            SideShare * MathF.Sin(_time * SideRate) * MathF.Sin(_time * SideSlow),
            MathF.Sin(_time * UpRate + UpPhase) * MathF.Cos(_time * UpSlow)) * shake;
    }
}
