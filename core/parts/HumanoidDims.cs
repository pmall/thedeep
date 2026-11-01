using System.Numerics;

namespace TheDeep;

/// <summary>
/// Sizes of a humanoid rig in the side plane, read from its joints' rest offsets (X forward, Y up).
/// </summary>
/// <param name="HipsHeight">Height of the hips joint above the feet at rest.</param>
/// <param name="HipJoint">Offset of the thigh joint from the hips joint (X, Y).</param>
/// <param name="Thigh">Thigh length, hip joint to knee joint.</param>
/// <param name="Shin">Shin length, knee joint to ankle joint.</param>
/// <param name="ShoulderHeight">Height of the shoulder joints above the hips joint at rest.</param>
/// <param name="UpperArm">Upper arm length, shoulder to elbow.</param>
/// <param name="Forearm">Forearm length, elbow to wrist.</param>
public readonly record struct HumanoidDims(float HipsHeight, Vector2 HipJoint, float Thigh, float Shin, float ShoulderHeight, float UpperArm, float Forearm)
{
    /// <summary>Height of the ankle joint above the sole when the leg hangs straight at rest.</summary>
    public float AnkleHeight => HipsHeight + HipJoint.Y - Thigh - Shin;
}
