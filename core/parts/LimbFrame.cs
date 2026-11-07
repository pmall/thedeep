namespace TheDeep;

/// <summary>
/// The bones of one limb segment (<see cref="LimbShapes.Frame"/>): a strut, the joint housing it turns on, and
/// what works it: a piston and a bundle of cables. Sizes in meters. "Across" is in the camera's plane, at right
/// angles to the limb: positive is forward of a limb hanging down, behind a limb pointing up.
/// </summary>
public sealed record LimbFrame
{
    /// <summary>The strut's width at the joint.</summary>
    public float Strut { get; init; } = 0.05f;

    /// <summary>The strut's width at its far end, as a share of <see cref="Strut"/>.</summary>
    public float StrutTaper { get; init; } = 0.8f;

    /// <summary>Radius of the joint housing at the origin, a drum whose axle faces the camera; 0 for none.</summary>
    public float Knuckle { get; init; } = 0.05f;

    /// <summary>The housing's depth toward the camera.</summary>
    public float KnuckleDepth { get; init; } = 0.1f;

    /// <summary>Radius of a smaller housing at the far end (a wrist, an ankle); 0 for none.</summary>
    public float EndKnuckle { get; init; }

    /// <summary>Radius of a piston's barrel along the strut; 0 for none.</summary>
    public float Piston { get; init; }

    /// <summary>How far across the strut the piston runs.</summary>
    public float PistonSide { get; init; }

    /// <summary>How many cables bow along the strut.</summary>
    public int Cables { get; init; }

    public float CableWidth { get; init; } = 0.012f;

    /// <summary>How far across the strut the cables bow at their middle.</summary>
    public float CableSide { get; init; }
}
