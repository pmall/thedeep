using System.Numerics;

namespace TheDeep;

/// <summary>
/// What held input means on a Mag-grip surface, pure. After a corner the input that led there keeps meaning "keep going" (even
/// if it now points away) until it changes (<see cref="Lock"/>); otherwise it moves along the surface, or lets go by pulling away.
/// </summary>
public sealed class GripSteer
{
    private Vector2? _locked;
    private float _lockedAlong;

    /// <summary>Forgets any lock (a fresh grip).</summary>
    public void Clear() => _locked = null;

    /// <summary>Keeps <paramref name="input"/> meaning <paramref name="along"/> (−1 or +1 along the surface) until the input changes.</summary>
    public void Lock(Vector2 input, float along)
    {
        _locked = input;
        _lockedAlong = along;
    }

    /// <summary>
    /// The movement along the surface (−1 to 1, <see cref="GripMath.Along"/>) that <paramref name="input"/> asks for on a surface
    /// with normal <paramref name="up"/>, or null when it pulls away past <paramref name="pullAway"/> (let go). A changed input drops the lock.
    /// </summary>
    public float? Along(Vector2 input, Vector2 up, float pullAway)
    {
        if (_locked is { } locked && locked == input)
        {
            return _lockedAlong;
        }

        _locked = null;
        return GripMath.PullsAway(input, up, pullAway) ? null : GripMath.Along(input, up);
    }
}
