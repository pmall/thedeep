using System.Numerics;

namespace TheDeep;

/// <summary>
/// What a brain wants this tick. The engine side moves the body toward <see cref="Velocity"/> within the
/// species' limits, and the channels drive the procedural animation (a joint bowing with
/// <see cref="Pray"/>, a blade rising with <see cref="Strike"/>, eyes warming with <see cref="Alert"/>).
/// Channels run from 0 to 1.
/// </summary>
public struct Intent
{
    /// <summary>Desired velocity, m/s. Walkers use X; flyers both; crawlers its part along their surface.</summary>
    public Vector2 Velocity;

    /// <summary>+1 or -1 to face that way; 0 faces the way it moves (or keeps the current facing).</summary>
    public int Facing;

    /// <summary>Where the sensors aim, if anywhere.</summary>
    public Vector2? LookAt;

    public MachineAct Act;

    /// <summary>Broadcast to neighbors (<see cref="Neighbor.Signal"/>).</summary>
    public float Signal;

    /// <summary>The strike lands this tick.</summary>
    public bool Hit;

    /// <summary>Devotion: bowing, kneeling, a censer swinging.</summary>
    public float Pray;

    /// <summary>A strike: wind-up up to about 0.6, the blow at 1, then back down.</summary>
    public float Strike;

    /// <summary>Threat: eyes turning the hazard color, a body rising to its full height.</summary>
    public float Alert;

    /// <summary>Awake versus dormant: a sleeper unfolding, a rotor spinning up.</summary>
    public float Rouse;

    /// <summary>Busy hands: pecking at scrap, working a tool.</summary>
    public float Work;
}
