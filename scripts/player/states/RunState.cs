namespace TheDeep.States;

/// <summary>Running on the ground toward the held direction.</summary>
public sealed class RunState : PlayerState
{
    public RunState(Player player) : base(player)
    {
    }

    public override PlayerState? PhysicsUpdate(float delta)
    {
        if (Player.GripAttach() is { } grip)
        {
            return grip;
        }

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

        return Player.CurrentInput.MoveX == 0 ? Player.States.Idle : null;
    }
}
