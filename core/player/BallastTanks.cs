namespace TheDeep;

/// <summary>Ballast's tanks after a tick in water (<see cref="BallastTanks.Step"/>): whether they are full, and whether the jump press was spent blowing them.</summary>
public readonly record struct BallastStep(bool Sunk, bool SpentJump);

/// <summary>Ballast's tanks, pure (docs/gameplay.md#ballast): holding down fills them, up or jump blows them.</summary>
public static class BallastTanks
{
    /// <summary>
    /// One tick in water. Without Ballast the tanks are empty. Holding down (<paramref name="moveY"/> below zero) fills them; with
    /// them full, up or a jump press (<paramref name="jumpPressed"/>) blows them, and the jump press is spent doing it.
    /// </summary>
    public static BallastStep Step(bool hasBallast, bool sunk, int moveY, bool jumpPressed)
    {
        if (!hasBallast)
        {
            return new BallastStep(false, false);
        }

        if (moveY < 0)
        {
            return new BallastStep(true, false);
        }

        if (sunk && (moveY > 0 || jumpPressed))
        {
            return new BallastStep(false, true);
        }

        return new BallastStep(sunk, false);
    }
}
