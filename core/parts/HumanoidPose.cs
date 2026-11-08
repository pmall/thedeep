using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A humanoid pose drawn in the side plane: where the hips are and every bone's absolute angle around Z (0 =
/// rest, positive turns counterclockwise as the camera sees a body facing right: a hanging limb swings
/// forward, an upright spine leans back), and each bone's absolute turn out of that plane (<see cref="Turn"/>; zero
/// unless set). A bone's whole rotation is its side-plane angle, then its roll, then its yaw. The rig turns these into
/// joint rotations.
/// </summary>
public sealed class HumanoidPose
{
    private readonly float[] _angles = new float[Humanoid.BoneCount];
    private readonly float[] _yaw = new float[Humanoid.BoneCount];
    private readonly float[] _roll = new float[Humanoid.BoneCount];

    /// <summary>Hips joint offset from its rest position, meters (X forward, Y up).</summary>
    public Vector2 HipsOffset { get; set; }

    public float this[HumanoidBone bone]
    {
        get => _angles[(int)bone];
        set => _angles[(int)bone] = value;
    }

    /// <summary>
    /// The bone's absolute turn out of the side plane, radians: <c>Yaw</c> around the up axis (positive brings its near, +Z,
    /// side forward), <c>Roll</c> around the forward axis (positive lowers its near side).
    /// </summary>
    public (float Yaw, float Roll) Turn(HumanoidBone bone) => (_yaw[(int)bone], _roll[(int)bone]);

    public void SetTurn(HumanoidBone bone, float yaw, float roll)
    {
        _yaw[(int)bone] = yaw;
        _roll[(int)bone] = roll;
    }

    /// <summary>
    /// Where the hip joint on <paramref name="side"/> (+1 the near, R, one; -1 the far one) is in the side plane (X forward, Y
    /// up from the soles), carried by the hips' offset, angle and turn: where a leg reaching for a planted foot starts.
    /// </summary>
    public Vector2 HipJoint(HumanoidDims dims, int side)
    {
        int hips = (int)HumanoidBone.Hips;
        Vector2 planar = PlanarIk.Rotate(dims.HipJoint, _angles[hips]);
        float z = side < 0 ? -dims.HipSpread : dims.HipSpread;
        float roll = _roll[hips], yaw = _yaw[hips];
        float y = planar.Y * MathF.Cos(roll) - z * MathF.Sin(roll);
        float zRolled = planar.Y * MathF.Sin(roll) + z * MathF.Cos(roll);
        float x = planar.X * MathF.Cos(yaw) + zRolled * MathF.Sin(yaw);
        return new Vector2(HipsOffset.X + x, dims.HipsHeight + HipsOffset.Y + y);
    }

    public void CopyFrom(HumanoidPose other)
    {
        Array.Copy(other._angles, _angles, _angles.Length);
        Array.Copy(other._yaw, _yaw, _yaw.Length);
        Array.Copy(other._roll, _roll, _roll.Length);
        HipsOffset = other.HipsOffset;
    }

    /// <summary>Blends toward <paramref name="other"/> by <paramref name="t"/> (0 keeps this pose, 1 takes the other).</summary>
    public void BlendToward(HumanoidPose other, float t)
    {
        if (t <= 0f)
        {
            return;
        }

        for (int i = 0; i < _angles.Length; i++)
        {
            Blend(other, i, t);
        }

        HipsOffset = Vector2.Lerp(HipsOffset, other.HipsOffset, t);
    }

    /// <summary>Blends the spine, head and arms toward <paramref name="other"/>, leaving the hips and legs (an upper-body move over the gait).</summary>
    public void BlendUpperToward(HumanoidPose other, float t)
    {
        if (t <= 0f)
        {
            return;
        }

        for (int i = 0; i < _angles.Length; i++)
        {
            HumanoidBone bone = (HumanoidBone)i;
            if (bone is HumanoidBone.Spine or HumanoidBone.Chest or HumanoidBone.Neck or HumanoidBone.Head
                or >= HumanoidBone.UpperArmR)
            {
                Blend(other, i, t);
            }
        }
    }

    /// <summary>Blends the hips (their angle, turn and offset) and the legs toward <paramref name="other"/>, leaving the rest.</summary>
    public void BlendLowerToward(HumanoidPose other, float t)
    {
        if (t <= 0f)
        {
            return;
        }

        for (int i = 0; i < _angles.Length; i++)
        {
            if ((HumanoidBone)i is HumanoidBone.Hips or (>= HumanoidBone.ThighR and <= HumanoidBone.FootL))
            {
                Blend(other, i, t);
            }
        }

        HipsOffset = Vector2.Lerp(HipsOffset, other.HipsOffset, t);
    }

    /// <summary>Sets <paramref name="bone"/> (its angle and turn) between <paramref name="a"/>'s and <paramref name="b"/>'s.</summary>
    public void SetBetween(HumanoidBone bone, HumanoidPose a, HumanoidPose b, float t)
    {
        int i = (int)bone;
        _angles[i] = a._angles[i] + (b._angles[i] - a._angles[i]) * t;
        _yaw[i] = a._yaw[i] + (b._yaw[i] - a._yaw[i]) * t;
        _roll[i] = a._roll[i] + (b._roll[i] - a._roll[i]) * t;
    }

    private void Blend(HumanoidPose other, int i, float t)
    {
        _angles[i] += (other._angles[i] - _angles[i]) * t;
        _yaw[i] += (other._yaw[i] - _yaw[i]) * t;
        _roll[i] += (other._roll[i] - _roll[i]) * t;
    }
}
