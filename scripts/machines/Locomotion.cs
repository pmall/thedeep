namespace TheDeep;

/// <summary>How a machine's body moves (the engine side of the movement vocabulary).</summary>
public enum Locomotion
{
    /// <summary>On the ground under gravity, along X: legs, treads.</summary>
    Walk,

    /// <summary>Free in the play plane, no gravity: rotors, fins.</summary>
    Hover,

    /// <summary>Clinging to the faces of the metal block it starts on, around its corners (mag-feet, mag-treads).</summary>
    Crawl,
}
