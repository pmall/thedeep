namespace TheDeep.States;

/// <summary>Airborne and not rising: past the apex, or walked off a ledge (coyote time applies).</summary>
public sealed class FallState : PlayerState
{
    public FallState(Player player) : base(player)
    {
    }

    public override MotionMode Motion => MotionMode.Air;

    public override PlayerState? PhysicsUpdate(float delta)
    {
        if (Player.GripAttach() is { } grip)
        {
            return grip;
        }

        Player.ApplyRunControl(delta, grounded: false);
        Player.ApplyGravity(delta);

        if (Player.WantsDash)
        {
            return Player.States.Dash;
        }

        if (Player.WantsJump)
        {
            return Player.States.Jump;
        }

        if (Player.IsOnFloor())
        {
            return Player.CurrentInput.MoveX != 0 ? Player.States.Run : Player.States.Idle;
        }

        return null;
    }
}
