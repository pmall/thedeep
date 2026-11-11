using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The trail a blade's sweep leaves, Godot-free: its edge (where the blade starts and its tip, in the world) recorded each
/// physics tick and filled in between ticks along the arc both ends swing around a pivot (the shoulder), so a cut that turns
/// the blade half a circle in two ticks still draws a round arc. Each sample fades with its age and the strength it was laid
/// with, which falls with the tip's speed under <see cref="FullSpeed"/> (a blade nearly still lays almost nothing, so the end
/// of a follow-through doesn't pile its samples into one bright spot); the oldest are dropped. Fixed buffers: stepping and reading allocate nothing.
/// </summary>
public sealed class BladeTrail
{
    private readonly Vector3[] _base;
    private readonly Vector3[] _tip;
    private readonly float[] _age;
    private readonly float[] _strength;
    private int _first;
    private Vector3 _lastPivot;
    private Vector3 _lastBase;
    private Vector3 _lastTip;
    private float _lastStrength;
    private bool _hasLast;

    /// <param name="lifetime">How long a sample lasts, seconds.</param>
    /// <param name="steps">Samples laid per tick along the arc.</param>
    /// <param name="capacity">The most samples kept; the oldest go first.</param>
    /// <param name="fullSpeed">The tip's speed (m/s) from which the trail is laid at its full strength.</param>
    public BladeTrail(float lifetime, int steps, int capacity, float fullSpeed)
    {
        Lifetime = MathF.Max(lifetime, 0.001f);
        FullSpeed = MathF.Max(fullSpeed, 0.001f);
        Steps = Math.Max(steps, 1);
        capacity = Math.Max(capacity, Steps + 1);
        _base = new Vector3[capacity];
        _tip = new Vector3[capacity];
        _age = new float[capacity];
        _strength = new float[capacity];
    }

    /// <summary>How long a sample lasts before it has faded away, seconds.</summary>
    public float Lifetime { get; }

    /// <summary>How many samples are laid per physics tick along the blade's arc.</summary>
    public int Steps { get; }

    /// <summary>The tip's speed from which the trail is laid at full strength, m/s; slower lays it fainter.</summary>
    public float FullSpeed { get; }

    /// <summary>How many samples are alive, oldest first.</summary>
    public int Count { get; private set; }

    /// <summary>Sample <paramref name="i"/> (0 the oldest): its two ends and how much of it is left, 0 to 1.</summary>
    public (Vector3 Base, Vector3 Tip, float Life) this[int i]
    {
        get
        {
            int k = (_first + i) % _base.Length;
            return (_base[k], _tip[k], _strength[k] * (1f - _age[k] / Lifetime));
        }
    }

    /// <summary>
    /// One tick: ages the trail and, while the blade lays it (<paramref name="strength"/> above 0, or it just was), fills the arc
    /// from the last tick's edge to this one, around <paramref name="pivot"/>.
    /// </summary>
    public void Step(float delta, Vector3 pivot, Vector3 bladeBase, Vector3 tip, float strength)
    {
        float dt = MathF.Max(delta, 0f);
        for (int i = 0; i < Count; i++)
        {
            _age[(_first + i) % _base.Length] += dt;
        }

        while (Count > 0 && _age[_first] >= Lifetime)
        {
            _first = (_first + 1) % _base.Length;
            Count--;
        }

        float speed = _hasLast && dt > 0f ? Vector3.Distance(tip, _lastTip) / dt : 0f;
        strength = Math.Clamp(strength, 0f, 1f) * Math.Clamp(speed / FullSpeed, 0f, 1f);
        if (_hasLast && (strength > 0f || _lastStrength > 0f))
        {
            for (int k = 1; k <= Steps; k++)
            {
                float s = k / (float)Steps;
                Vector3 at = Vector3.Lerp(_lastPivot, pivot, s);
                Add(Arc(at, _lastBase - _lastPivot, bladeBase - pivot, s), Arc(at, _lastTip - _lastPivot, tip - pivot, s),
                    dt * (1f - s), _lastStrength + (strength - _lastStrength) * s);
            }
        }

        _lastPivot = pivot;
        _lastBase = bladeBase;
        _lastTip = tip;
        _lastStrength = strength;
        _hasLast = true;
    }

    /// <summary>Drops every sample and the last edge (a teleport).</summary>
    public void Clear()
    {
        Count = 0;
        _first = 0;
        _hasLast = false;
        _lastStrength = 0f;
    }

    private void Add(Vector3 bladeBase, Vector3 tip, float age, float strength)
    {
        if (Count == _base.Length)
        {
            _first = (_first + 1) % _base.Length;
            Count--;
        }

        int k = (_first + Count) % _base.Length;
        _base[k] = bladeBase;
        _tip[k] = tip;
        _age[k] = age;
        _strength[k] = strength;
        Count++;
    }

    /// <summary>A point swung from <paramref name="from"/> to <paramref name="to"/> (both from the pivot) by <paramref name="s"/>: turned, not cut across.</summary>
    private static Vector3 Arc(Vector3 pivot, Vector3 from, Vector3 to, float s)
    {
        float a = from.Length(), b = to.Length();
        float length = a + (b - a) * s;
        if (a < 1e-5f || b < 1e-5f)
        {
            return pivot + Vector3.Lerp(from, to, s);
        }

        Vector3 u = from / a, v = to / b;
        float cos = Math.Clamp(Vector3.Dot(u, v), -1f, 1f);
        float angle = MathF.Acos(cos);
        float sin = MathF.Sin(angle);
        Vector3 direction = sin < 1e-3f
            ? Vector3.Lerp(u, v, s)
            : (MathF.Sin((1f - s) * angle) * u + MathF.Sin(s * angle) * v) / sin;
        return pivot + direction * length;
    }
}
