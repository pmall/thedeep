namespace TheDeep.States;

/// <summary>
/// In water deep enough to swim (<see cref="WaterMotion"/>). Floating, the body rides its float line with the head above the
/// surface, swims across at SwimSpeed, and a jump near the float line leaves the water. With Ballast, holding down fills the
/// tanks (<see cref="Player.Sunk"/>): the body sinks and walks the bottom; up or jump blows them and it rises back to float.
/// A dash works as on land (sunk, with the Drill, down and dash dives); the harpoon fires from the water. Wading into water
/// too shallow to swim stands it up again.
/// </summary>
public sealed class SwimState : PlayerState
{
    /// <summary>The swim state of <paramref name="player"/>, made once by <see cref="PlayerStateSet"/>.</summary>
    public SwimState(Player player) : base(player)
    {
    }

    public override MotionMode Motion => MotionModes.Water(Player.Sunk, Player.IsOnFloor(), Player.Velocity.Y, Player.Water.RiseSpeed);

    public override void Enter()
    {
        Player.BeginSwim();
    }

    public override PlayerState? PhysicsUpdate(float delta)
    {
        if (Player.HarpoonFire() is { } harpoon)
        {
            return harpoon;
        }

        if (Player.WaterSurface is not { } surface)
        {
            return Player.States.Fall; // swum out of the water's side
        }

        PlayerInput input = Player.CurrentInput;
        if (!Player.Abilities.Has(Ability.Ballast))
        {
            Player.Sunk = false;
        }
        else if (input.MoveY < 0)
        {
            Player.Sunk = true;
        }
        else if (Player.Sunk && (input.MoveY > 0 || Player.JumpPressed))
        {
            Player.Sunk = false;
            Player.ConsumeJump();
        }

        WaterLimits w = Player.Water;
        float feet = Player.GlobalPosition.Y;
        Player.Velocity = new Godot.Vector3(
            WaterMotion.Horizontal(Player.Velocity.X, input.MoveX, w, delta),
            WaterMotion.Vertical(Player.Velocity.Y, feet, surface, Player.Sunk, w, delta),
            0f);

        if (!Player.Sunk && Player.JumpPressed && WaterMotion.CanJumpOut(feet, surface, w))
        {
            return Player.States.Jump;
        }

        if (Player.WantsDash)
        {
            return Player.DashMove();
        }

        if (Player.IsOnFloor() && !WaterMotion.Deep(feet, surface, w))
        {
            return input.MoveX != 0 ? Player.States.Run : Player.States.Idle;
        }

        return null;
    }
}
