namespace TheDeep;

/// <summary>
/// A machine's behavior, Godot-free: each physics tick it turns a <see cref="Perception"/> into an
/// <see cref="Intent"/>. Brains combine the vocabulary (<see cref="Senses"/>, <see cref="Steering"/>,
/// <see cref="PrayerCycle"/>, <see cref="StrikeAction"/>); a species with a truly new behavior gets a new
/// brain. A brain is created by its species' behavior resource (MachineBehavior) with that resource's knobs.
/// </summary>
public abstract class MachineBrain
{
    /// <summary>What the machine was doing at the last tick.</summary>
    public MachineAct Act { get; protected set; }

    public abstract Intent Think(in Perception p, float delta);
}
