using Godot;

namespace TheDeep;

/// <summary>
/// A harpoon anchor: an iron mooring ring hung in the air of a room, a hoop lit in the Harpoon's color set in it
/// (<see cref="GateKinds"/>), hollow so it never reads as a power-up's stone. With the Harpoon the pilgrim fires at it and zips to it
/// (<see cref="States.HarpoonState"/>). The node's origin is the point the pilgrim's center is pulled to; keep a body's height of air
/// below it. It shows its state (<see cref="Look"/>): low without the Harpoon, half lit out of range, lit in range; the one the
/// harpoon would fire at has the sight's marks closed on it, flares when the line bites and jolts on its shackle, and stays dim once
/// zipped to until it can be fired at again. Visual and a position only: no collision.
/// </summary>
public partial class HarpoonAnchor : Node3D
{
    /// <summary>The ring, hung from its shackle: its pivot is the shackle, so it sways and jolts there.</summary>
    [Export] public Node3D Ring { get; set; } = null!;

    /// <summary>The lit hoop (token_glow).</summary>
    [Export] public GeometryInstance3D Hoop { get; set; } = null!;

    /// <summary>The sight's marks (token_glow), shown round the ring the harpoon would fire at.</summary>
    [Export] public GeometryInstance3D Sight { get; set; } = null!;

    [Export] public OmniLight3D Light { get; set; } = null!;

    /// <summary>How lit the hoop is in each look (1 is the interactables' lit glow, <see cref="TokenGlow"/>).</summary>
    [Export] public float DormantGlow { get; set; } = 0.35f;
    [Export] public float FarGlow { get; set; } = 0.55f;
    [Export] public float ReadyGlow { get; set; } = 1f;
    [Export] public float SightedGlow { get; set; } = 1.8f;
    [Export] public float BittenGlow { get; set; } = 3f;
    [Export] public float SpentGlow { get; set; } = 0.18f;

    /// <summary>How far the sight's marks stand out from the ring before they close on it (share of their radius).</summary>
    [Export] public float SightSpread { get; set; } = 0.7f;

    /// <summary>The state it shows, set each tick by <see cref="HarpoonRig"/>.</summary>
    public AnchorLook Look
    {
        get => _look;
        set
        {
            if (value == AnchorLook.Bitten && _look != AnchorLook.Bitten)
            {
                _jolt = 1f;
            }

            _look = value;
        }
    }

    /// <summary>The point the pilgrim's center is pulled to, on the play plane.</summary>
    public System.Numerics.Vector2 Point => new(GlobalPosition.X, GlobalPosition.Y);

    private AnchorLook _look;
    private float _baseEnergy;
    private float _glow;
    private float _sight;
    private float _jolt;
    private float _time;

    public override void _Ready()
    {
        Color color = GateKinds.ColorOf(Ability.Harpoon);
        Light.LightColor = color;
        _baseEnergy = Light.LightEnergy;
        TokenGlow.Tint(Hoop, color);
        TokenGlow.Tint(Sight, color);
        _time = GlobalPosition.X * 0.7f;
        _glow = GlowOf(_look);
        Present(0f);
    }

    public override void _Process(double delta)
    {
        Present((float)delta);
    }

    private void Present(float dt)
    {
        _time += dt;
        float target = GlowOf(_look);
        // Eased: up quickly, down a little slower, so a flare spent on a zip is dim before the pilgrim fires again.
        _glow = Mathf.Lerp(target, _glow, Mathf.Exp(-dt * (target > _glow ? 14f : 8f)));
        bool aimed = _look is AnchorLook.Sighted or AnchorLook.Bitten;
        _sight = Motion.Approach(_sight, aimed ? 1f : 0f, dt * (aimed ? 7f : 4f));
        _jolt = Motion.Approach(_jolt, 0f, dt * 2.5f);

        float breathe = 0.9f + 0.1f * Mathf.Sin(_time * 2.2f);
        TokenGlow.Light(Hoop, _glow * breathe);
        Light.LightEnergy = _baseEnergy * (0.25f + 0.75f * _glow * breathe);

        Sight.Visible = _sight > 0.01f;
        if (Sight.Visible)
        {
            float closed = _sight * _sight * (3f - 2f * _sight);
            // Closing in from SightSpread out; once closed it keeps a slow beat, and tightens on the ring when the line bites.
            float beat = _look == AnchorLook.Bitten ? -0.12f : 0.04f * Mathf.Sin(_time * 5f);
            Sight.Scale = Vector3.One * (1f + SightSpread * (1f - closed) + beat * closed);
            Sight.Rotation = new Vector3(0f, 0f, 0.6f * (1f - closed) + 0.15f * Mathf.Sin(_time * 0.9f) * closed);
            TokenGlow.Light(Sight, closed * (_look == AnchorLook.Bitten ? 2.2f : 1.4f));
        }

        // A slow sway on the shackle, and a jolt that rings out when the line bites.
        float sway = 0.035f * Mathf.Sin(_time * 1.1f) + 0.16f * _jolt * _jolt * Mathf.Sin(_time * 21f);
        Ring.Rotation = new Vector3(0f, 0f, sway);
    }

    private float GlowOf(AnchorLook look) => look switch
    {
        AnchorLook.Dormant => DormantGlow,
        AnchorLook.Far => FarGlow,
        AnchorLook.Ready => ReadyGlow,
        AnchorLook.Sighted => SightedGlow,
        AnchorLook.Bitten => BittenGlow,
        AnchorLook.Spent => SpentGlow,
        _ => ReadyGlow,
    };
}
