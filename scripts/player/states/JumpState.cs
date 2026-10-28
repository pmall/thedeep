namespace TheDeep.States;

/// <summary>Rising after a jump. Releasing jump early cuts the upward speed once (variable height).</summary>
public sealed class JumpState : PlayerState
{
    private bool _cut;

    public JumpState(Player player) : base(player)
    {
    }

    public override MotionMode Motion => MotionMode.Air;

    public override void Enter()
    {
        Player.ConsumeJump();
        Player.Velocity = Player.Velocity with { Y = Tuning.JumpTakeOffSpeed };
        _cut = false;
    }

    public override PlayerState? PhysicsUpdate(float delta)
    {
        if (Player.HarpoonFire() is { } harpoon)
        {
            return harpoon;
        }

        if (Player.GripAttach() is { } grip)
        {
            return grip;
        }

        if (!_cut && !Player.CurrentInput.JumpHeld)
        {
            Player.Velocity = Player.Velocity with { Y = Player.Velocity.Y * Tuning.JumpCutFactor };
            _cut = true;
        }

        Player.ApplyRunControl(delta, grounded: false);
        Player.ApplyGravity(delta);

        if (Player.WantsDash)
        {
            return Player.States.Dash;
        }

        return Player.Velocity.Y <= 0f || Player.IsOnCeiling() ? Player.States.Fall : null;
    }
}
