namespace TheDeep;

/// <summary>
/// The side camera's follow numbers (<see cref="CameraFollow"/>): the look-ahead (meters, the speed it starts at in m/s, its ease in
/// seconds), the eases of the follow (seconds, about how long it lags a steady target), the band a jump rises in before the camera
/// follows it (meters), the lead under a fast fall (meters, at the fall speeds where it starts and is full, m/s) and the leash
/// (meters): no ease ever lets the body farther than that from where the camera wants it.
/// </summary>
public sealed record FollowLimits(
    float LookAhead, float LookAheadMinSpeed, float LookAheadTime, float HorizontalTime, float VerticalTime, float RiseBand,
    float FallLead, float FallLeadFrom, float FallLeadFull, float LeashX, float LeashY);
