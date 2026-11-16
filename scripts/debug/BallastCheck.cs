using System.Globalization;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted water and Ballast check in the mechanics lab (`-- --room MechanicsLab --ballast-check`, run by tools/check.sh), through
/// the real input actions, in a tank of water it places itself: dropped in, the pilgrim splashes and settles floating on its float
/// line; without Ballast, holding down keeps it floating; with Ballast, holding down sinks it to the bottom, where it stays when let
/// go; up blows the tanks and it rises back to float; a jump near the float line leaves the water. Each step has a time limit.
/// Prints `BALLAST OK` or `BALLAST CHECK` errors.
/// </summary>
public partial class BallastCheck : StepDriver
{
    /// <summary>The running game's gate hints.</summary>
    public required GateHints Hints { get; init; }

    /// <summary>The tank: its surface, its middle, its floor (the lab's).</summary>
    private const float Surface = 5f, Middle = 12.5f, Bottom = 0f;

    private float Y => Player.GlobalPosition.Y;
    private float Line => WaterMotion.FloatLine(Surface, Player.Water);
    private bool Swimming => Player.CurrentState == Player.States.Swim;

    protected override void Build()
    {
        World.Room.AddChild(LabProps.Tank(new Vector2(4f, 4f), Surface - Bottom, new Vector3(Middle, Surface, 0f)));

        var hinted = new HintLog(Hints);
        Steps.Add(new("dropped in, it splashes and settles floating on the float line", 3f,
            () => { Player.Abilities.Lock(Ability.Ballast); Player.Respawn(new Vector3(Middle, Surface + 4f, 0f), 1); },
            () => Swimming && Mathf.Abs(Y - Line) < 0.1f && Mathf.Abs(Player.Velocity.Y) < 0.3f));
        Steps.Add(new("without Ballast, holding down keeps it floating", 1.5f,
            () => Press(InputActions.MoveDown),
            () => !Swimming || Y < Line - 0.5f || Player.Sunk, Hold: true));
        Steps.Add(new("and the pilgrim says it is too light to sink (the gate hint)", 1f,
            () => { },
            () => hinted.Said(Ability.Ballast)));
        Steps.Add(new("with Ballast, holding down sinks it to the bottom", 3f,
            () => Player.Abilities.Unlock(Ability.Ballast),
            () => Swimming && Player.Sunk && Player.IsOnFloor() && Y < Bottom + 0.05f));
        Steps.Add(new("let go, it stays on the bottom", 1f,
            () => Release(InputActions.MoveDown),
            () => !Player.Sunk || Y > Bottom + 0.05f, Hold: true));
        Steps.Add(new("up blows the tanks: it rises back to float", 3f,
            () => Press(InputActions.MoveUp),
            () => Swimming && !Player.Sunk && Mathf.Abs(Y - Line) < 0.15f));
        Steps.Add(new("a jump near the float line leaves the water", 1f,
            () => { Release(InputActions.MoveUp); Press(InputActions.Jump); },
            () => Y > Surface + 0.3f && !Swimming));
    }

    protected override string Tag => "BALLAST";

    protected override string Detail =>
        $"at {Player.GlobalPosition}, velocity {Player.Velocity}, state {Player.CurrentState.Name}, sunk {Player.Sunk}, " +
        $"water {Player.WaterSurface?.ToString(CultureInfo.InvariantCulture) ?? "none"}, float line {Line}";

    protected override string Outcome =>
        $"{StepCount} steps (splashed in and floated, held up without it, the gate hint, sank, stayed down, rose, jumped out)";
}
