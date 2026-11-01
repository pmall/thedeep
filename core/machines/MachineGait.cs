using System;

namespace TheDeep;

/// <summary>
/// How a machine's body follows its movement, pure: the distance walked (treads roll and legs step by it), how much it is stepping
/// (eased in and out), the way it faces, and a walker's fall. Also the seed a machine takes from where it stands.
/// </summary>
public sealed class MachineGait
{
    /// <summary>Below this speed a machine is standing (m/s): stepping eases out.</summary>
    private const float StepSpeed = 0.3f;

    /// <summary>How fast stepping eases in and out, per second.</summary>
    private const float StepSharpness = 8f;

    /// <summary>A walker falls at this much per second per second, at most this fast, and presses this hard into a floor (m/s).</summary>
    public const float Gravity = 25f, MaxFallSpeed = 20f, FloorPress = 0.5f;

    /// <summary>The distance moved so far, meters.</summary>
    public float Walked { get; private set; }

    /// <summary>How much the machine is stepping, 0 (standing) to 1 (walking).</summary>
    public float Stepping { get; private set; }

    public void Reset()
    {
        Walked = 0f;
        Stepping = 0f;
    }

    /// <summary>One tick that moved the machine <paramref name="travelled"/> meters (signed); returns its speed (m/s).</summary>
    public float Moved(float travelled, float dt)
    {
        float speed = MathF.Abs(travelled) / dt;
        Walked += MathF.Abs(travelled);
        Stepping = Motion.Damp(Stepping, Math.Clamp(speed / StepSpeed, 0f, 1f), StepSharpness, dt);
        return speed;
    }

    /// <summary>
    /// The way a machine faces after a tick: as its brain asks (<paramref name="asked"/> ≠ 0), else the way it moved
    /// (<paramref name="travelled"/> along X or along its path, with <paramref name="forward"/> the sign that is forward), else as before.
    /// </summary>
    public static int Facing(int asked, float travelled, int forward, int current) =>
        asked != 0 ? asked : MathF.Abs(travelled) > 1e-3f ? Math.Sign(travelled) * forward : current;

    /// <summary>A walker's vertical speed after a tick: pressed into the floor it stands on, or falling.</summary>
    public static float Fall(float vy, bool onFloor, float dt) => onFloor ? -FloorPress : MathF.Max(vy - Gravity * dt, -MaxFallSpeed);

    /// <summary>A seed that varies a machine from others of its species, from where it stands (1 to 9973).</summary>
    public static int SeedAt(float x, float y)
    {
        float n = MathF.Round(x * 13f + y * 7f) % 9973f;
        return 1 + (int)(n < 0f ? n + 9973f : n);
    }
}
