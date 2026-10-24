using System;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// Poses a <see cref="PartBody"/> whose joints are named after <see cref="HumanoidBone"/>: reads its
/// sizes (<see cref="Dims"/>) from the rest offsets and applies <see cref="HumanoidPose"/>s to the joints.
/// </summary>
public sealed class HumanoidRig
{
    private readonly Node3D[] _joints = new Node3D[Humanoid.BoneCount];
    private readonly Vector3[] _rest = new Vector3[Humanoid.BoneCount];

    private HumanoidRig(HumanoidDims dims)
    {
        Dims = dims;
    }

    public HumanoidDims Dims { get; }

    /// <summary>The rig of <paramref name="body"/>, or null (with an error) if a humanoid joint is missing.</summary>
    public static HumanoidRig? From(PartBody body)
    {
        var joints = new Node3D[Humanoid.BoneCount];
        var rest = new Vector3[Humanoid.BoneCount];
        foreach (HumanoidBone bone in Enum.GetValues<HumanoidBone>())
        {
            var name = new StringName(bone.ToString());
            if (body.Joint(name) is not { } joint)
            {
                GD.PushError($"{body.Assembly.ResourcePath}: humanoid joint {bone} is missing");
                return null;
            }

            joints[(int)bone] = joint;
            rest[(int)bone] = body.RestOffset(name);
        }

        Vector3 R(HumanoidBone b) => rest[(int)b];
        var dims = new HumanoidDims(
            HipsHeight: R(HumanoidBone.Hips).Y,
            HipJoint: new NVector2(R(HumanoidBone.ThighR).X, R(HumanoidBone.ThighR).Y),
            Thigh: R(HumanoidBone.ShinR).Length(),
            Shin: R(HumanoidBone.FootR).Length(),
            ShoulderHeight: R(HumanoidBone.Spine).Y + R(HumanoidBone.Chest).Y + R(HumanoidBone.UpperArmR).Y,
            UpperArm: R(HumanoidBone.ForearmR).Length(),
            Forearm: R(HumanoidBone.HandR).Length());
        var rig = new HumanoidRig(dims);
        Array.Copy(joints, rig._joints, joints.Length);
        Array.Copy(rest, rig._rest, rest.Length);
        return rig;
    }

    /// <summary>Sets every joint from <paramref name="pose"/>. Call on physics ticks.</summary>
    public void Apply(HumanoidPose pose)
    {
        for (int i = 0; i < _joints.Length; i++)
        {
            var bone = (HumanoidBone)i;
            Vector3 position = _rest[i];
            if (bone == HumanoidBone.Hips)
            {
                position += new Vector3(pose.HipsOffset.X, pose.HipsOffset.Y, 0f);
            }

            _joints[i].Transform = new Transform3D(new Basis(Vector3.Back, pose.Local(bone)), position);
        }
    }
}
