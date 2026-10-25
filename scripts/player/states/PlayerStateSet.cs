namespace TheDeep.States;

/// <summary>The player's state instances, created once and reused.</summary>
public sealed class PlayerStateSet
{
    public PlayerStateSet(Player player)
    {
        Idle = new IdleState(player);
        Run = new RunState(player);
        Jump = new JumpState(player);
        Fall = new FallState(player);
        Dash = new DashState(player);
        Grip = new GripState(player);
    }

    public IdleState Idle { get; }
    public RunState Run { get; }
    public JumpState Jump { get; }
    public FallState Fall { get; }
    public DashState Dash { get; }
    public GripState Grip { get; }
}
