
namespace TheDeep;

/// <summary>The numbers of a <see cref="LawStrain"/> (lengths in meters, times in seconds, rates per second).</summary>
public readonly record struct LawStrainTuning(
    float Near, float Lip, float BehindLip, float Height, float PushStrain, float PushTime, float ShowRate, float ReleaseRate, float BreakTime);
