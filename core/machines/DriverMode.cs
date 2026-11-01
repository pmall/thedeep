namespace TheDeep;

/// <summary>How a <see cref="JointDriver"/> moves its joint.</summary>
public enum DriverMode
{
    /// <summary>Turns by Angle (and moves by Offset) in proportion to its channel: bowing, raising a blade.</summary>
    Pose,

    /// <summary>Swings back and forth by Angle (and Offset) on its clock, scaled by its channel: a censer, a bob.</summary>
    Wave,

    /// <summary>Turns continuously at Angle degrees per second, scaled by its channel: rotors, wheels, drills.</summary>
    Spin,

    /// <summary>Turns around Z (at most Angle degrees) so its forward (+X) points at what the brain looks at: eyes, heads.</summary>
    Aim,
}
