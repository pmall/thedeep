namespace TheDeep;

/// <summary>
/// How fast the pilgrim's walking clips travel at their own rate, m/s (each clip's root travel over its length): what
/// <see cref="PilgrimAnimation"/> matches their rates to the body's speed with, so the feet (or the strokes) keep pace.
/// </summary>
public readonly record struct ClipSpeeds(float Run, float Grip, float Swim);
