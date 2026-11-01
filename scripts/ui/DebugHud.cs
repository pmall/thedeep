using System.Text;
using Godot;

namespace TheDeep;

/// <summary>
/// Playtesting readouts. In debug builds, the room's name and where the pilgrim stands in it (meters, the room's own frame) sit in
/// the top right corner at all times, so a remark (or an F12 screenshot) says where it was made, and the whole controls manual
/// sits bottom left, lit while held, for the device in hand (<see cref="ControlsManual"/>; F8 hides it). F7 adds the movement
/// state, velocity and abilities. Release builds show nothing.
/// </summary>
public partial class DebugHud : CanvasLayer
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public World World { get; set; } = null!;

    private readonly StringBuilder _text = new();
    private Label _info = null!;
    private Label _room = null!;

    /// <summary>The on-screen controls manual (built in <see cref="_Ready"/>, debug builds only; null in release).</summary>
    public ControlsManual? Manual { get; private set; }

    public override void _Ready()
    {
        _info = GetNode<Label>("%Info");
        _room = GetNode<Label>("%Room");
        _info.Visible = false;
        Visible = OS.IsDebugBuild();
        if (Visible)
        {
            Manual = new ControlsManual { Player = Player };
            AddChild(Manual);
            Manual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.DebugToggleHud) && OS.IsDebugBuild())
        {
            _info.Visible = !_info.Visible;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.DebugToggleManual) && Manual is not null)
        {
            Manual.Visible = !Manual.Visible;
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
