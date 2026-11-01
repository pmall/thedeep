using System.Text;
using Godot;

namespace TheDeep;

/// <summary>
/// Playtesting readouts. In debug builds, the room's name and where the pilgrim stands in it (meters, the room's own frame) sit in
/// the top right corner at all times, so a remark (or an F12 screenshot) says where it was made. F7 adds the movement state,
/// velocity and abilities, and the controls. Release builds show nothing.
/// </summary>
public partial class DebugHud : CanvasLayer
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public World World { get; set; } = null!;

    private readonly StringBuilder _text = new();
    private Label _info = null!;
    private Label _controls = null!;
    private Label _room = null!;

    public override void _Ready()
    {
        _info = GetNode<Label>("%Info");
        _controls = GetNode<Label>("%Controls");
        _room = GetNode<Label>("%Room");
        _info.Visible = _controls.Visible = false;
        Visible = OS.IsDebugBuild();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.DebugToggleHud) && OS.IsDebugBuild())
        {
            _info.Visible = _controls.Visible = !_info.Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        if (World.RoomName != "")
        {
            Vector3 at = World.Room.ToLocal(Player.GlobalPosition);
            _room.Text = $"{World.RoomName}   {at.X:0.0}, {at.Y:0.0}";
        }

        if (!_info.Visible)
        {
            return;
        }

        Vector3 v = Player.Velocity;
        _text.Clear();
        _text.Append("state  ").Append(Player.CurrentState.Name).Append('\n');
        _text.Append("vel    ").Append(v.X.ToString("+0.0;-0.0")).Append(", ").Append(v.Y.ToString("+0.0;-0.0")).Append('\n');
        _text.Append("floor  ").Append(Player.IsOnFloor() ? "yes" : "no").Append('\n');
        _text.Append("dash   ").Append(Player.DashCooldownRemaining > 0f ? $"cooldown {Player.DashCooldownRemaining:0.00}s" : "ready")
             .Append(", air dashes ").Append(Player.AirDashesLeft).Append('\n');
        _text.Append("water  ").Append(Player.WaterSurface is { } surface ? $"surface {surface:0.0}, {(Player.Sunk ? "sunk" : "floating")}" : "none")
             .Append('\n');
        _text.Append("abilities ");
        foreach (Ability ability in AbilitySet.All)
        {
            _text.Append(Player.Abilities.Has(ability) ? "[x] " : "[ ] ").Append(ability).Append("  ");
        }

        _info.Text = _text.ToString();
    }
}
