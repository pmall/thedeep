using System.Text;
using Godot;

namespace TheDeep;

/// <summary>
/// Playtesting readouts. In debug builds, the room's name and where the pilgrim stands in it (meters, the room's own frame) sit in
/// the top right corner at all times, so a remark (or an F12 screenshot) says where it was made, and the gameplay controls sit
/// bottom left, lit while held, for the device in hand (<see cref="ControlsManual"/>; F8 hides them). F7 adds the movement
/// state, velocity and abilities, and the debug keys. Release builds show nothing.
/// </summary>
public partial class DebugHud : CanvasLayer
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public World World { get; set; } = null!;

    private readonly StringBuilder _text = new();
    private Label? _info;
    private Label? _room;

    // The scene's unique labels.
    private Label Info => _info ??= GetNode<Label>("%Info");
    private Label RoomLabel => _room ??= GetNode<Label>("%Room");

    /// <summary>The gameplay controls (built in <see cref="_Ready"/>, debug builds only; null in release).</summary>
    public ControlsManual? Manual { get; private set; }

    /// <summary>The debug keys, shown with the F7 readout (debug builds only).</summary>
    public ControlsManual? DebugKeys { get; private set; }

    public override void _Ready()
    {
        Info.Visible = false;
        Visible = OS.IsDebugBuild();
        if (Visible)
        {
            Manual = new ControlsManual { Player = Player };
            DebugKeys = new ControlsManual { Player = Player, Specs = ControlsManual.Debug, BothDevices = true, Top = true, Visible = false };
            foreach (ControlsManual manual in new[] { Manual, DebugKeys })
            {
                AddChild(manual);
                manual.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.DebugToggleHud) && OS.IsDebugBuild())
        {
            Info.Visible = !Info.Visible;
            if (DebugKeys is not null)
            {
                DebugKeys.Visible = Info.Visible;
            }

            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(InputActions.DebugToggleManual) && Manual is not null)
        {
            Manual.Visible = !Manual.Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    private (string, int, int) _roomShown;

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        if (World.RoomName != "")
        {
            Vector3 at = World.Room.ToLocal(Player.GlobalPosition);
            var shown = (World.RoomName, Mathf.RoundToInt(at.X * 10f), Mathf.RoundToInt(at.Y * 10f));
            if (shown != _roomShown)
            {
                // Rebuilt only when what it shows changes: a new string every frame is garbage.
                _roomShown = shown;
                RoomLabel.Text = $"{World.RoomName}   {at.X:0.0}, {at.Y:0.0}";
            }
        }

        if (!Info.Visible)
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

        Info.Text = _text.ToString();
    }
}
