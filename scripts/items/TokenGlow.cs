using Godot;

namespace TheDeep;

/// <summary>
/// The per-node knobs of the interactables' glow (assets/shaders/token_glow.gdshader, instance uniforms): the color of its kind, and
/// how lit it is: lit while it waits to be used, flaring as it is used, spent after.
/// </summary>
public static class TokenGlow
{
    /// <summary>How lit a spent glow stays: a dark ember, so the piece still reads as the same kind, used.</summary>
    public const float Spent = 0.06f;

    private static readonly StringName TintParameter = "tint";
    private static readonly StringName GlowParameter = "glow";

    /// <summary>Sets the color of <paramref name="glow"/>: its kind's.</summary>
    public static void Tint(GeometryInstance3D glow, Color color) => glow.SetInstanceShaderParameter(TintParameter, color);

    /// <summary>Sets how lit <paramref name="glow"/> is: 1 waiting to be used, <see cref="Spent"/> once used.</summary>
    public static void Light(GeometryInstance3D glow, float lit) => glow.SetInstanceShaderParameter(GlowParameter, lit);

    /// <summary>On <paramref name="tween"/> (in parallel with what it plays): flares <paramref name="glow"/> to <paramref name="peak"/>, then fades it to spent.</summary>
    public static void FlareOut(Tween tween, GeometryInstance3D glow, float peak, float seconds)
    {
        tween.Parallel().TweenMethod(Callable.From<float>(t =>
        {
            // Up fast in the first fifth, then down eased.
            float lit = t < 0.2f ? Mathf.Lerp(1f, peak, t / 0.2f) : Mathf.Lerp(peak, Spent, Mathf.Ease((t - 0.2f) / 0.8f, 0.5f));
            Light(glow, lit);
        }), 0f, 1f, seconds);
    }
}
