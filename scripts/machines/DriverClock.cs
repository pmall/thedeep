namespace TheDeep;

/// <summary>What a waving <see cref="JointDriver"/> keeps time with.</summary>
public enum DriverClock
{
    /// <summary>Seconds: Rate is in waves per second.</summary>
    Time,

    /// <summary>Meters walked: Rate is in waves per meter, so the wave keeps step with the legs.</summary>
    Gait,
}
