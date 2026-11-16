namespace TheDeep;

/// <summary>Ballast's tanks after a tick in water (<see cref="BallastTanks.Step"/>): whether they are full, and whether the jump press was spent blowing them.</summary>
public readonly record struct BallastStep(bool Sunk, bool SpentJump);
