namespace TheDeep;

/// <summary>
/// The bones a humanoid rig must have for <see cref="HumanoidPose"/> (the pilgrim, and any humanoid machine).
/// A part assembly names its joints after them ("ThighR"). R is the side toward +Z, the near side when
/// the body faces +X.
/// </summary>
public enum HumanoidBone
{
    Hips,
    Spine,
    Chest,
    Neck,
    Head,
    ThighR,
    ShinR,
    FootR,
    ThighL,
    ShinL,
    FootL,
    UpperArmR,
    ForearmR,
    HandR,
    UpperArmL,
    ForearmL,
    HandL,
}
