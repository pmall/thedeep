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
    private readonly int[] _parent = new int[Humanoid.BoneCount];

    /// <summary>Each bone's whole rotation in the body's frame, for the pose being applied (kept, so applying allocates nothing).</summary>
    private readonly Basis[] _whole = new Basis[Humanoid.BoneCount];

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
            HipJoint: R(HumanoidBone.ThighR).Flat(),
            HipSpread: R(HumanoidBone.ThighR).Z,
            Thigh: R(HumanoidBone.ShinR).Length(),
            Shin: R(HumanoidBone.FootR).Length(),
            ShoulderHeight: R(HumanoidBone.Spine).Y + R(HumanoidBone.Chest).Y + R(HumanoidBone.UpperArmR).Y,
            UpperArm: R(HumanoidBone.ForearmR).Length(),
            Forearm: R(HumanoidBone.HandR).Length());
        var rig = new HumanoidRig(dims);
        Array.Copy(joints, rig._joints, joints.Length);
        Array.Copy(rest, rig._rest, rest.Length);
        foreach (HumanoidBone bone in Enum.GetValues<HumanoidBone>())
        {
            rig._parent[(int)bone] = Humanoid.Parent(bone) is { } parent ? (int)parent : -1;
        }

        return rig;
    }

    /// <summary>
    /// Sets every joint from <paramref name="pose"/>: each bone's whole rotation (its side-plane angle, then its roll, then its
    /// yaw; <see cref="HumanoidPose.Turn"/>) taken relative to its parent's. Call on physics ticks.
    /// </summary>
    public void Apply(HumanoidPose pose)
    {
        for (int i = 0; i < _joints.Length; i++) // parents come before their children in HumanoidBone
        {
            var bone = (HumanoidBone)i;
            (float yaw, float roll) = pose.Turn(bone);
            _whole[i] = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, roll) * new Basis(Vector3.Back, pose[bone]);
            Basis local = _parent[i] < 0 ? _whole[i] : _whole[_parent[i]].Transposed() * _whole[i];
            Vector3 position = _rest[i];
            if (bone == HumanoidBone.Hips)
            {
                position += pose.HipsOffset.OnPlane();
            }

            _joints[i].Transform = new Transform3D(local, position);
        }
    }
}
