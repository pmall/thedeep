using System.Text;
using Godot;

namespace TheDeep;

/// <summary>Corner readout of the player's movement state and abilities, for playtesting.</summary>
public partial class DebugHud : CanvasLayer
{
    [Export] public Player Player { get; set; } = null!;

    private readonly StringBuilder _text = new();
    private Label _info = null!;

    public override void _Ready()
    {
        _info = GetNode<Label>("%Info");
    }

    public override void _Process(double delta)
    {
        Vector3 v = Player.Velocity;
        _text.Clear();
        _text.Append("state  ").Append(Player.CurrentState.Name).Append('\n');
        _text.Append("vel    ").Append(v.X.ToString("+0.0;-0.0")).Append(", ").Append(v.Y.ToString("+0.0;-0.0")).Append('\n');
        _text.Append("floor  ").Append(Player.IsOnFloor() ? "yes" : "no").Append('\n');
        _text.Append("dash   ").Append(Player.DashCooldownRemaining > 0f ? $"cooldown {Player.DashCooldownRemaining:0.00}s" : "ready")
             .Append(", air dashes ").Append(Player.AirDashesLeft).Append('\n');
        _text.Append("abilities ");
        foreach (Ability ability in AbilitySet.All)
        {
            _text.Append(Player.Abilities.Has(ability) ? "[x] " : "[ ] ").Append(ability).Append("  ");
        }

        _info.Text = _text.ToString();
    }
}
