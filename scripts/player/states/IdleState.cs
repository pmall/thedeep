namespace TheDeep.States;

/// <summary>Standing on the ground; decelerates to a stop.</summary>
public sealed class IdleState : PlayerState
{
    public IdleState(Player player) : base(player)
    {
    }

    public override PlayerState? PhysicsUpdate(float delta)
    {
        Player.ApplyRunControl(delta, grounded: true);
        Player.ApplyGravity(delta);

        if (Player.WantsDash)
        {
            return Player.States.Dash;
        }

        if (Player.WantsJump)
        {
            return Player.States.Jump;
        }

        if (!Player.IsOnFloor())
        {
            return Player.States.Fall;
        }

        return Player.CurrentInput.MoveX != 0 ? Player.States.Run : null;
    }
}
