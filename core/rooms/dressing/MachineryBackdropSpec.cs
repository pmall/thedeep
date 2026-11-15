namespace TheDeep;

/// <summary>
/// The numbers of a machinery backdrop (<see cref="MachineryBackdrop"/>): sizes in meters per unit of the row's scale (a
/// share is of the row's pitch or of the room's band, as its name says).
/// </summary>
public sealed record MachineryBackdropSpec
{
    /// <summary>The approved look.</summary>
    public static readonly MachineryBackdropSpec Default = new();

    /// <summary>A gear's mean radius, as a share of the pitch.</summary>
    public float GearRadiusShare { get; init; } = 0.85f;

    /// <summary>A piston's radius.</summary>
    public float PistonRadius { get; init; } = 0.45f;

    /// <summary>A gear's middle stands at least this share of the room's band over its floor...</summary>
    public float GearHeightShare { get; init; } = 0.3f;

    /// <summary>...and what it may add to that.</summary>
    public float GearHeightSpread { get; init; } = 0.4f;

    /// <summary>A gear takes at least this share of the mean radius...</summary>
    public float GearSizeShare { get; init; } = 0.75f;

    /// <summary>...and what it may add to that.</summary>
    public float GearSizeSpread { get; init; } = 0.4f;

    /// <summary>How far in front of the pistons a gear stands, so they pass behind it.</summary>
    public float GearDepth { get; init; } = 2f;

    /// <summary>How far behind its gear's plane a hub's origin sits.</summary>
    public float HubBack { get; init; } = 0.6f;

    /// <summary>A backdrop gear's teeth.</summary>
    public int GearTeeth { get; init; } = 28;

    /// <summary>A backdrop gear's thickness, times the row's scale.</summary>
    public float GearThickness { get; init; } = 0.5f;

    /// <summary>A hub's length along its axle, times the row's scale.</summary>
    public float HubLength { get; init; } = 1.2f;

    /// <summary>A hub's radius, times the row's scale.</summary>
    public float HubRadius { get; init; } = 1.1f;

    /// <summary>A hub's column has this many sides...</summary>
    public int HubSides { get; init; } = 12;

    /// <summary>...a plinth this high, times the row's scale...</summary>
    public float HubPlinth { get; init; } = 0.3f;

    /// <summary>...and a capital this high, times the row's scale.</summary>
    public float HubCapital { get; init; } = 0.3f;

    /// <summary>A piston's column has this many sides...</summary>
    public int PistonSides { get; init; } = 10;

    /// <summary>...a plinth this high, times the row's scale...</summary>
    public float PistonPlinth { get; init; } = 0.8f;

    /// <summary>...and a capital this high, times the row's scale.</summary>
    public float PistonCapital { get; init; } = 0.5f;
}
