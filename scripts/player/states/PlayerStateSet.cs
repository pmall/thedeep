namespace TheDeep.States;

/// <summary>The player's state instances, created once and reused.</summary>
public sealed class PlayerStateSet
{
    /// <summary>Makes every state of <paramref name="player"/>.</summary>
    public PlayerStateSet(Player player)
    {
        Idle = new IdleState(player);
        Run = new RunState(player);
        Jump = new JumpState(player);
        Fall = new FallState(player);
        Dash = new DashState(player);
        Grip = new GripState(player);
        Harpoon = new HarpoonState(player);
        Dive = new DiveState(player);
        Swim = new SwimState(player);
    }

    /// <summary>Standing on the ground.</summary>
    public IdleState Idle { get; }
    /// <summary>Running on the ground.</summary>
    public RunState Run { get; }
    /// <summary>Rising after a jump.</summary>
    public JumpState Jump { get; }
    /// <summary>Airborne and not rising.</summary>
    public FallState Fall { get; }
    /// <summary>The horizontal burst.</summary>
    public DashState Dash { get; }
    /// <summary>Mag-grip: walking on a metal wall or ceiling.</summary>
    public GripState Grip { get; }
    /// <summary>The Harpoon's flight to an anchor.</summary>
    public HarpoonState Harpoon { get; }
    /// <summary>The Drill's dive, straight down.</summary>
    public DiveState Dive { get; }
    /// <summary>In water deep enough to swim.</summary>
    public SwimState Swim { get; }
}
