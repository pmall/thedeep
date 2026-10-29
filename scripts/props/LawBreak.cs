using System;
using Godot;

namespace TheDeep;

/// <summary>
/// One of the three laws holding the pilgrim back (docs/theme.md): at the lip of a lethal drop (the third law), the
/// pilgrim's own refusal stands as a veil of glyphs it cannot walk through, and the law's words rise as it nears.
/// Pressing on toward the drop strains against it; held long enough (<see cref="BreakTime"/>, slipping back when
/// let go), the law breaks: the veil scatters, the words go out, and the way is open for good (<see cref="Session"/>
/// remembers it by this node's room and name, and calls <see cref="Settle"/> on later visits). A story beat, not an
/// ability gate: the veil is not a <see cref="Block"/>, and the map counts the way as open.
/// </summary>
public partial class LawBreak : Node3D
{
    [Export] public StaticBody3D Fence { get; set; } = null!;
    [Export] public Label3D Words { get; set; } = null!;
    [Export] public Node3D Veil { get; set; } = null!;
    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>The law's words (shown at the lip), and the banner when it breaks.</summary>
    [Export] public StringName LawLine { get; set; } = "";
    [Export] public StringName BrokenLine { get; set; } = "";
    [Export] public StringName BrokenHintLine { get; set; } = "";

    /// <summary>Which way the forbidden side lies from the node (+1 east, −1 west).</summary>
    [Export] public int Toward { get; set; } = 1;

    /// <summary>The words start to show this far from the lip, meters.</summary>
    [Export] public float Near { get; set; } = 6f;

    /// <summary>Seconds of pressing on before the law breaks; letting go slips back at half that rate.</summary>
    [Export] public float BreakTime { get; set; } = 2.6f;

    /// <summary>The words' color before they show: the dark of the room around the lip.</summary>
    private static readonly Color Dark = new(0.12f, 0.11f, 0.16f);

    private float _strain;
    private float _shown;
    private float _time;
    private float _gone = -1f;
    private float _baseEnergy;
    private Vector3 _wordsAt;

    /// <summary>The pilgrim, set by the session when the room loads.</summary>
    public Player? Player { get; set; }

    public bool Broken => _gone >= 0f;

    /// <summary>0 to 1 while the pilgrim presses against the law.</summary>
    public float Strain => _strain;

    public event Action<LawBreak>? Broke;

    public override void _Ready()
    {
        Fence.CollisionLayer = PhysicsLayers.WorldRock;
        Fence.CollisionMask = 0;
        Fence.Position = new Vector3(Toward * 0.25f, 0f, 0f);
        Words.Text = LineBook.Shared.Find(LawLine)?.Text ?? "";
        _wordsAt = Words.Position;
        _baseEnergy = Light.LightEnergy;
        Apply();
    }

    /// <summary>Broken before: the way is open, nothing shows.</summary>
    public void Settle()
    {
        _gone = 10f;
        _strain = 0f;
        Fence.ProcessMode = ProcessModeEnum.Disabled;
        Apply();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        if (Broken)
        {
            _gone += dt;
            Apply();
            return;
        }

        float away = float.PositiveInfinity;
        bool pressing = false;
        if (Player is { } player)
        {
            away = (GlobalPosition.X - player.GlobalPosition.X) * Toward;
            pressing = player.InControl && player.IsOnFloor() && away is > -0.2f and < 1.2f
                       && player.CurrentInput.MoveX == Toward && Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) < 1.5f;
        }

        _shown = Motion.Approach(_shown, away < Near && away > -1f ? 1f : 0f, dt * 1.5f);
        _strain = Mathf.Clamp(_strain + (pressing ? dt / BreakTime : -dt * 0.5f / BreakTime), 0f, 1f);
        if (_strain >= 1f)
        {
            _gone = 0f;
            Fence.ProcessMode = ProcessModeEnum.Disabled;
            Broke?.Invoke(this);
        }

        Apply();
    }

    private void Apply()
    {
        float s = _strain * _strain;
        // Breaking: the words flare white and drift up as they go out; the veil scatters upward.
        float gone = Broken ? Mathf.Clamp(_gone / 1.2f, 0f, 1f) : 0f;
        float shown = _shown * _shown * (3f - 2f * _shown) * (1f - gone);
        float tremble = s * 0.05f;
        Words.Position = _wordsAt + new Vector3(Mathf.Sin(_time * 41f) * tremble, Mathf.Sin(_time * 29f) * tremble + gone * 1.5f, 0f);
        // The words write depth (alpha-discarded, so depth of field keeps them sharp): they fade in by color instead.
        Words.Visible = shown > 0.02f;
        Words.Modulate = Dark.Lerp(new Color(0.86f + 0.14f * s, 0.84f + 0.16f * s, 1f), shown);
        Veil.Visible = gone < 1f;
        Veil.Scale = new Vector3(1f + 0.6f * s + 2f * gone, 1f + 0.3f * gone, 1f);
        Veil.Position = new Vector3(0f, 1.5f * gone, 0f);
        Light.LightEnergy = _baseEnergy * (0.3f + 0.7f * shown + 3f * s) * (1f - gone) + (Broken && gone < 1f ? _baseEnergy * 6f * (1f - gone) : 0f);
    }
}
