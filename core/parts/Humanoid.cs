using System;

namespace TheDeep;

/// <summary>The humanoid skeleton's shape (Godot-free): which bone hangs from which, and the sizes the gait needs.</summary>
public static class Humanoid
{
    public static readonly int BoneCount = Enum.GetValues<HumanoidBone>().Length;

    /// <summary>The bone each bone hangs from; null for the hips (the root).</summary>
    public static HumanoidBone? Parent(HumanoidBone bone) => bone switch
    {
        HumanoidBone.Hips => null,
        HumanoidBone.Spine => HumanoidBone.Hips,
        HumanoidBone.Chest => HumanoidBone.Spine,
        HumanoidBone.Neck => HumanoidBone.Chest,
        HumanoidBone.Head => HumanoidBone.Neck,
        HumanoidBone.ThighR or HumanoidBone.ThighL => HumanoidBone.Hips,
        HumanoidBone.ShinR => HumanoidBone.ThighR,
        HumanoidBone.FootR => HumanoidBone.ShinR,
        HumanoidBone.ShinL => HumanoidBone.ThighL,
        HumanoidBone.FootL => HumanoidBone.ShinL,
        HumanoidBone.UpperArmR or HumanoidBone.UpperArmL => HumanoidBone.Chest,
        HumanoidBone.ForearmR => HumanoidBone.UpperArmR,
        HumanoidBone.HandR => HumanoidBone.ForearmR,
        HumanoidBone.ForearmL => HumanoidBone.UpperArmL,
        HumanoidBone.HandL => HumanoidBone.ForearmL,
        _ => throw new ArgumentOutOfRangeException(nameof(bone)),
    };
}
