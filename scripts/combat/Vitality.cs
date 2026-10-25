using System;

namespace TheDeep;

/// <summary>
/// Health with a grace period: a hit takes health, then nothing hurts for <see cref="GraceTime"/> seconds.
/// Pure, used by the pilgrim (and by machines without the grace period).
/// </summary>
public sealed class Vitality
{
    public Vitality(int max, float graceTime = 0f)
    {
        Max = Math.Max(1, max);
        Current = Max;
        GraceTime = graceTime;
    }

    public int Max { get; private set; }

    public int Current { get; private set; }

    public float GraceTime { get; }

    /// <summary>Seconds left in the grace period after a hit.</summary>
    public float Grace { get; private set; }

    public bool Dead => Current <= 0;

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
