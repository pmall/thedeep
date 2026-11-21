namespace TheDeep;

/// <summary>
/// When a foot plants, read from where the animation puts the soles: a sole that comes down to the ground after being
/// lifted off it is a footfall (its grit, its sound). Godot-free; heights are meters above the ground the body stands on.
/// </summary>
public sealed class Footfalls
{
    /// <summary>A sole this low is on the ground, meters.</summary>
    public const float Planted = 0.035f;

    /// <summary>A sole must rise this high before it can plant again, meters: no footfalls from a foot rolling in place.</summary>
    public const float Lifted = 0.08f;

    private bool _rightUp;
    private bool _leftUp;

    /// <summary>
    /// One tick: the right sole's and the left's heights over the ground, and whether the body is walking on it. Returns
    /// the foot that planted, +1 right, -1 left, 0 none (both at once: the right).
    /// </summary>
    public int Update(float right, float left, bool walking)
    {
        int planted = 0;
        if (!walking)
        {
            _rightUp = _leftUp = false;
            return 0;
        }

        if (_leftUp && left <= Planted)
        {
            _leftUp = false;
            planted = -1;
        }

        if (_rightUp && right <= Planted)
        {
            _rightUp = false;
            planted = 1;
        }

        _rightUp |= right >= Lifted;
        _leftUp |= left >= Lifted;
        return planted;
    }
}
