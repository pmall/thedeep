namespace TheDeep.States;

/// <summary>
/// One movement state. Each physics tick the active state sets the player's velocity and returns
/// the state to switch to (or null to stay); the player then moves.
/// </summary>
public abstract class PlayerState
{
    protected PlayerState(Player player)
    {
        Player = player;
        Name = GetType().Name.Replace("State", "");
    }

    /// <summary>The state's name without "State" (Idle, Dash), for readouts and check reports.</summary>
    public string Name { get; }

    /// <summary>Whether held input may change the player's facing while in this state.</summary>
    public virtual bool AllowsTurning => true;

    /// <summary>Whether this state bores through cracked rock ahead of the body when the pilgrim has the Drill.</summary>
    public virtual bool Bores => false;

    /// <summary>What the animation shows in this state.</summary>
    public virtual MotionMode Motion => MotionMode.Ground;

    protected Player Player { get; }

    protected PlayerTuning Tuning => Player.Tuning;

    /// <summary>Called as the player switches to this state.</summary>
    public virtual void Enter()
    {
    }

    /// <summary>Called as the player leaves this state.</summary>
    public virtual void Exit()
    {
    }

    /// <summary>One physics tick of <paramref name="delta"/> seconds in this state: returns the state to switch to, or null to
    /// stay; the player then moves.</summary>
    public abstract PlayerState? PhysicsUpdate(float delta);

    /// <summary>After the player moved this tick: the state to switch to (or null), for states that react to contacts.</summary>
    public virtual PlayerState? AfterMove() => null;
}
