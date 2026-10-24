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

    public string Name { get; }

    /// <summary>Whether held input may change the player's facing while in this state.</summary>
    public virtual bool AllowsTurning => true;

    protected Player Player { get; }

    protected PlayerTuning Tuning => Player.Tuning;

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public abstract PlayerState? PhysicsUpdate(float delta);
}
