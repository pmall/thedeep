using Godot;

namespace TheDeep;

/// <summary>
/// Movement feedback around the pilgrim, visual only: grit from footsteps on rock and sparks on metal (the
/// floor's physics layer decides), dust when landing or dashing off the ground, and the dash trail.
/// The camera's shake on hard landings lives in SideCamera.
/// </summary>
public partial class PlayerEffects : Node
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public BurstPool Grit { get; set; } = null!;
    [Export] public BurstPool Puff { get; set; } = null!;
    [Export] public BurstPool Sparks { get; set; } = null!;
    [Export] public DashTrail Trail { get; set; } = null!;

    /// <summary>Where the trail is drawn from: the body's center.</summary>
    [Export] public Node3D TrailAnchor { get; set; } = null!;

    /// <summary>Landing speed (m/s) below which a landing raises no dust.</summary>
    [Export] public float SoftLanding { get; set; } = 4f;

    /// <summary>Landing speed at which the dust is at its fullest.</summary>
    [Export] public float HardLanding { get; set; } = 18f;

    public override void _Ready()
    {
        Player.Landed += OnLanded;
        Player.Dashed += OnDashed;
        Player.Respawned += Trail.Clear;
        Player.Visual.Footfall += OnFootfall;
    }

    public override void _ExitTree()
    {
        Player.Landed -= OnLanded;
        Player.Dashed -= OnDashed;
        Player.Respawned -= Trail.Clear;
        Player.Visual.Footfall -= OnFootfall;
    }

    public override void _PhysicsProcess(double delta)
    {
        Trail.Track(TrailAnchor, Player.CurrentState.Motion == MotionMode.Dash, (float)delta);
    }

    private void OnFootfall(Vector3 position, int side)
    {
        float strength = Player.Visual.Gait;
        if (Player.FloorSurface == SurfaceKind.Metal)
        {
            Sparks.Emit(position, 0.35f * strength);
        }
        else
        {
            Grit.Emit(position, strength);
        }
    }

    private void OnLanded(float impactSpeed)
    {
        float strength = Mathf.Clamp((impactSpeed - SoftLanding) / (HardLanding - SoftLanding), 0f, 1f);
        Vector3 feet = Player.GlobalPosition;
        Puff.Emit(feet, strength);
        if (Player.FloorSurface == SurfaceKind.Metal)
        {
            Sparks.Emit(feet, strength);
        }
    }

    private void OnDashed(int direction)
    {
        if (Player.IsOnFloor())
        {
            Puff.Emit(Player.GlobalPosition, 0.45f);
        }
    }
}
