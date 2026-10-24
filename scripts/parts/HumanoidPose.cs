using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A humanoid pose in the side plane: where the hips are and every bone's absolute angle around Z (0 =
/// rest, positive turns counterclockwise as the camera sees a body facing right: a hanging limb swings
/// forward, an upright spine leans back). The rig turns these into joint rotations.
/// </summary>
public sealed class HumanoidPose
{
    private readonly float[] _angles = new float[Humanoid.BoneCount];

    /// <summary>Hips joint offset from its rest position, meters (X forward, Y up).</summary>
    public Vector2 HipsOffset { get; set; }

    public float this[HumanoidBone bone]
    {
        get => _angles[(int)bone];
        set => _angles[(int)bone] = value;
    }

    /// <summary>The bone's rotation relative to its parent bone, what its joint gets.</summary>
    public float Local(HumanoidBone bone) =>
        Humanoid.Parent(bone) is { } parent ? _angles[(int)bone] - _angles[(int)parent] : _angles[(int)bone];

    public void CopyFrom(HumanoidPose other)
    {
        Array.Copy(other._angles, _angles, _angles.Length);
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
            _angles[i] += (other._angles[i] - _angles[i]) * t;
        }

        HipsOffset = Vector2.Lerp(HipsOffset, other.HipsOffset, t);
    }
}
