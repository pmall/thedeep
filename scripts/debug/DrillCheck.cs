using Godot;

namespace TheDeep;

/// <summary>
/// Scripted Drill check in the mechanics lab (`-- --room MechanicsLab --drill-check`, run by tools/check.sh), through the
/// real input actions, against cracked blocks it places itself: without the Drill a dash stops at cracked rock and leaves
/// it whole; with it the dash bores through and carries on; down and dash on cracked rock dives through it to the floor
/// below; down and dash in the air dives and stops on sound rock (unbroken); down and dash on sound rock is a dash. Each
/// step has a time limit. Prints `DRILL OK` or `DRILL CHECK` errors.
/// </summary>
public partial class DrillCheck : StepDriver
{
    /// <summary>The running game's gate hints.</summary>
    public required GateHints Hints { get; init; }

    private bool _dove;
    // A cracked wall across the floor, and a cracked slab in the air over it.
    private readonly Block _wall = LabProps.Block("CrackedWall", new Vector3(13f, 1.5f, 0f), new Vector3(1f, 3f, 4f), cracked: true);
    private readonly Block _slab = LabProps.Block("CrackedSlab", new Vector3(20f, 3.25f, 0f), new Vector3(4f, 0.5f, 4f), cracked: true);

    private float X => Player.GlobalPosition.X;
    private float Y => Player.GlobalPosition.Y;

    protected override void Build()
    {
        World.Room.AddChild(_wall);
        World.Room.AddChild(_slab);

        var hinted = new HintLog(Hints);
        Steps.Add(new("without the Drill, a dash stops at cracked rock and leaves it whole", 0.6f,
            () => { Player.Abilities.Lock(Ability.Drill); Place(10.5f, 0f); Press(InputActions.Dash); },
            () => _wall.Broken || X > 12.5f, Hold: true));
        Steps.Add(new("pressed against it, the pilgrim says it would give to a bore (the gate hint)", 1.5f,
            () => { Release(InputActions.Dash); Press(InputActions.MoveRight); },
            () => hinted.Said(Ability.Drill)));
        Steps.Add(new("with it, the dash bores through and carries on", 0.5f,
            () => { Release(InputActions.MoveRight); Player.Abilities.Unlock(Ability.Drill); Place(10.5f, 0f); Press(InputActions.Dash); },
            () => _wall.Broken && X > 14.5f && Player.CurrentState == Player.States.Dash));
        Steps.Add(new("down and dash on cracked rock: the dive breaks it and lands on the floor below", 1f,
            () => { Release(InputActions.Dash); Place(20f, 3.5f); Press(InputActions.MoveDown); Press(InputActions.Dash); },
            () => _slab.Broken && _dove && Player.IsOnFloor() && Y < 0.05f && Player.CurrentState != Player.States.Dive));
        Steps.Add(new("down and dash in the air: a dive, stopped by sound rock", 1.2f,
            () => { Release(InputActions.Dash); _dove = false; Place(6f, 3f); Press(InputActions.Dash); },
            () => _dove && Player.IsOnFloor() && Y < 0.05f && Y > -0.05f && Player.CurrentState != Player.States.Dive));
        Steps.Add(new("down and dash on sound rock: a dash", 0.3f,
            () => { Release(InputActions.Dash); _dove = false; Place(3f, 0f); Press(InputActions.Dash); },
            () => Player.CurrentState == Player.States.Dash && !_dove));
    }

    protected override string Tag => "DRILL";

    protected override void Observe(float delta) => _dove |= Player.CurrentState == Player.States.Dive;

    protected override string Detail =>
        $"at {Player.GlobalPosition}, state {Player.CurrentState.Name}, wall {(_wall.Broken ? "broken" : "whole")}, " +
        $"slab {(_slab.Broken ? "broken" : "whole")}, dove {_dove}";

    protected override string Outcome =>
        $"{StepCount} steps (stopped without it, the gate hint, bored through, dove through, dove onto sound rock, a dash on sound rock)";
}
