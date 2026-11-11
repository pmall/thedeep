using System;

namespace TheDeep;

/// <summary>
/// Health with a grace period: a hit takes health, then nothing hurts for <see cref="GraceTime"/> seconds.
/// Pure, used by the pilgrim (and by machines without the grace period).
/// </summary>
public sealed class Vitality
{
    /// <summary>Full health at <paramref name="max"/> (at least 1), with <paramref name="graceTime"/> seconds of grace after
    /// each hit.</summary>
    public Vitality(int max, float graceTime = 0f)
    {
        Max = Math.Max(1, max);
        Current = Max;
        GraceTime = graceTime;
    }

    /// <summary>Full health, in health points; at least 1.</summary>
    public int Max { get; private set; }

    /// <summary>Health left, in health points, from 0 (dead) to <see cref="Max"/>.</summary>
    public int Current { get; private set; }

    /// <summary>Seconds after a hit during which nothing hurts; 0 for none.</summary>
    public float GraceTime { get; }

    /// <summary>Seconds left in the grace period after a hit.</summary>
    public float Grace { get; private set; }

    /// <summary>Whether no health is left.</summary>
    public bool Dead => Current <= 0;

    /// <summary>Whether a hit would hurt now: alive and out of the grace period.</summary>
    public bool Vulnerable => !Dead && Grace <= 0f;

    /// <summary>Raised when health changes, with the new value.</summary>
    public event Action<int>? Changed;

    /// <summary>Takes <paramref name="amount"/> health unless in the grace period or dead; returns whether it hurt.</summary>
    public bool Damage(int amount)
    {
        if (!Vulnerable || amount <= 0)
        {
            return false;
        }

        Current = Math.Max(0, Current - amount);
        Grace = GraceTime;
        Changed?.Invoke(Current);
        return true;
    }

    /// <summary>All health gone at once, grace or not (swallowed by molten slag); returns whether it was still alive.</summary>
    public bool Kill()
    {
        if (Dead)
        {
            return false;
        }

        Current = 0;
        Changed?.Invoke(Current);
        return true;
    }

    /// <summary>Runs the grace period down by <paramref name="delta"/> seconds.</summary>
    public void Tick(float delta) => Grace = Math.Max(0f, Grace - delta);

    /// <summary>Back to full health, no grace period.</summary>
    public void Restore()
    {
        Current = Max;
        Grace = 0f;
        Changed?.Invoke(Current);
    }

    /// <summary>A new maximum (a health upgrade), restoring to it.</summary>
    public void SetMax(int max)
    {
        Max = Math.Max(1, max);
        Restore();
    }
}
