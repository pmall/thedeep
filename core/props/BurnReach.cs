namespace TheDeep;

/// <summary>Where a body stands against something that burns it (heat it isn't plated for, slag), on the play plane (<see cref="Burn.Reach"/>).</summary>
public enum BurnReach
{
    /// <summary>Farther than the margin: the ground under it may be footing.</summary>
    Clear,

    /// <summary>Within the margin but not touching: no burn, and the ground here is no footing to be set back on.</summary>
    Near,

    /// <summary>Sharing area with it: it burns.</summary>
    Touching,
}
