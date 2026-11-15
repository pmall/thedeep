namespace TheDeep;

/// <summary>Where a melee swing is in its timing: ready for the next, winding up, hitting, or recovering.</summary>
public enum SwingPhase
{
    Ready,
    WindUp,
    Active,
    Recover,
}
