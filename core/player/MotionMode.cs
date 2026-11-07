namespace TheDeep;

/// <summary>What the body is doing, as the pilgrim's animation sees it (from the movement state, picked by <see cref="MotionModes"/>).</summary>
public enum MotionMode
{
    Ground,

    /// <summary>Walking on a Mag-grip surface (wall, ceiling or floor alike, in the body's own frame): clinging low.</summary>
    Grip,
    Air,

    /// <summary>A dash begun on the ground: the low lunge.</summary>
    Dash,

    /// <summary>A dash begun in the air (or the water): the body stretched flat behind it, legs together.</summary>
    AirDash,

    /// <summary>A dash with the Drill into cracked rock: tucked tight behind both arms, a spear.</summary>
    Bore,

    /// <summary>In water, floating (sunk on the bottom walks: <see cref="Ground"/>).</summary>
    Swim,

    /// <summary>In water with Ballast's tanks full, going down feet first.</summary>
    Sink,

    /// <summary>In water, the tanks blown, rising toward the float line.</summary>
    Rise,

    /// <summary>The Drill's dive, straight down.</summary>
    Dive,

    /// <summary>The Harpoon's line flying out, before it bites: the arm casting it toward <see cref="PilgrimMotion.Reach"/>.</summary>
    Throw,

    /// <summary>The Harpoon's pull along the bitten line, toward <see cref="PilgrimMotion.Reach"/>.</summary>
    Zip,
}
