namespace TheDeep;

/// <summary>What the body is doing, as the pilgrim's animation sees it (from the movement state).</summary>
public enum MotionMode
{
    Ground,
    Air,
    Dash,

    /// <summary>In water, floating or sinking and rising (sunk on the bottom walks: <see cref="Ground"/>).</summary>
    Swim,

    /// <summary>The Drill's dive, straight down.</summary>
    Dive,

    /// <summary>The Harpoon: the line thrown and the pull along it, toward <see cref="PilgrimMotion.Reach"/>.</summary>
    Zip,
}
