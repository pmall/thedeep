
namespace TheDeep;

/// <summary>What a lift is doing (<see cref="LiftMotion"/>).</summary>
public enum LiftPhase
{
    /// <summary>At its lower stop, empty.</summary>
    Waiting,

    /// <summary>At its lower stop with a rider, about to let go of its brake.</summary>
    Boarding,

    /// <summary>On its way up.</summary>
    Rising,

    /// <summary>At its upper stop.</summary>
    Arrived,

    /// <summary>Empty, sinking back to its lower stop.</summary>
    Returning,
}
