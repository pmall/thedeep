
namespace TheDeep;

/// <summary>Where a law stands (<see cref="LawStrain"/>).</summary>
public enum LawState
{
    /// <summary>Holding.</summary>
    Intact,

    /// <summary>Just broken: the veil is scattering and the words going out.</summary>
    Breaking,

    /// <summary>Broken for good (also a law broken on an earlier visit).</summary>
    Broken,
}
