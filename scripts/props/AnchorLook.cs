namespace TheDeep;

/// <summary>
/// What a harpoon anchor shows of its state to the pilgrim (<see cref="HarpoonAnchor.Look"/>, set each tick by <see cref="HarpoonRig"/>):
/// how lit its hoop is and whether the sight closes on it. A look only: what the Harpoon can fire at is the rig's.
/// </summary>
public enum AnchorLook
{
    /// <summary>The pilgrim lacks the Harpoon: the ring glows low, still its color, so the gate reads.</summary>
    Dormant,

    /// <summary>Out of the Harpoon's range (or too close to fire at): half lit.</summary>
    Far,

    /// <summary>In range, not the one the harpoon would fire at: lit.</summary>
    Ready,

    /// <summary>The one the harpoon fires at now, or its line flying to it: bright, the sight closed on it.</summary>
    Sighted,

    /// <summary>The line has bitten and pulls the pilgrim to it: it flares, the sight tight on it, the ring jolts.</summary>
    Bitten,

    /// <summary>Just zipped to: dim until the pilgrim stands or grips again, when it can be fired at again.</summary>
    Spent,
}
