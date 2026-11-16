using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The Sacristy's hidden passage in place (`-- --room Sacristy --secret-check`, run by tools/check.sh), through the real input
/// actions: the false wall stands and its way is hidden on the map; walking west into it finds it (it fades, remembered); walking on
/// crosses into the Cell; walking back, the wall is gone for good. Each step has a time limit. Prints `SECRET OK` or `SECRET CHECK`
/// errors.
/// </summary>
public partial class SecretCheck : StepDriver
{
    /// <summary>The running game's hidden passages (<see cref="TheDeep.Secrets"/>).</summary>
    public required Secrets Secrets { get; init; }
    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }

    private bool _found;

    private FalseWall? Wall => World.Room.First<FalseWall>();

    protected override void Build()
    {
        Secrets.Found += _ => _found = true;
        Steps.Add(new("the false wall stands, and the way behind it is hidden on the map", 0.5f,
            () => { },
            () => Wall is { Found: false } && World.Map.WaysOf("Sacristy").Any(w => w.Hidden && w.Exit == "West")));
        Steps.Add(new("walking west into it finds it", 3f,
            () => { Player.Respawn(new Vector3(5f, 0f, 0f), -1); Press(InputActions.MoveLeft); },
            () => _found && Wall is { Found: true } && Session.Done("Sacristy", Wall.NameText())));
        Steps.Add(new("walking on crosses into the Cell", 3f, () => { }, () => World.RoomName == "Cell" && Player.InControl));
        Steps.Add(new("walking back, the wall is gone for good", 5f,
            () => { Release(InputActions.MoveLeft); Press(InputActions.MoveRight); },
            () => World.RoomName == "Sacristy" && Wall is { Found: true }));
    }

    protected override string Tag => "SECRET";

    protected override string Detail => $"in {World.RoomName} at {Player.GlobalPosition}, found {_found}";

    protected override string Outcome =>
        $"{StepCount} steps (a false wall and a hidden way, found by walking in, through to the Cell, gone for good)";
}
