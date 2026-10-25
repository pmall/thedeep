namespace TheDeep;

/// <summary>
/// What a machine is visibly doing: the behavior vocabulary's actions and states, as a player would name
/// them. Brains report one every tick; the bestiary will record the ones the player has watched.
/// </summary>
public enum MachineAct
{
    Idle,
    Pray,
    Wander,
    Glean,
    Flee,
    Cower,
    Hover,
    Follow,
    Startle,
    Sleep,
    Wake,
    Hunt,
    WindUp,
    Strike,
    Recover,
    Watch,
}
