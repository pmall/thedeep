namespace TheDeep;

/// <summary>
/// Derives gravity and take-off speed from designer-friendly jump numbers
/// (apex height and time to reach it). Pure logic, unit tested.
/// </summary>
public static class JumpMath
{
    /// <summary>Gravity (m/s², positive) that makes a jump peak at <paramref name="height"/> after <paramref name="timeToApex"/>.</summary>
    public static float Gravity(float height, float timeToApex) => 2f * height / (timeToApex * timeToApex);

    /// <summary>Upward take-off speed (m/s) for the same jump.</summary>
    public static float TakeOffSpeed(float height, float timeToApex) => 2f * height / timeToApex;
}
