using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A machine's condition, pure: its health, the stagger a blow leaves (the brain stunned, a wind-up lost), the flash of the hit,
/// and its breaking: once its health is gone it folds down over <see cref="BreakTime"/> and is gone until the room is entered again.
/// </summary>
public sealed class MachineVitals
{
    /// <summary>A broken machine folds down and sinks over this long, seconds.</summary>
    public const float BreakTime = 0.6f;

    /// <summary>How fast a hit's flash fades, per second.</summary>
    private const float FlashFade = 5f;

    private readonly Vitality _health;
    private readonly float _staggerTime;
    private float _stagger;
    private float _breaking = -1f;

    /// <param name="health">Hits it takes.</param>
    /// <param name="staggerTime">How long a hit stuns its brain, seconds; 0 for a machine that shrugs hits off (a boss).</param>
    public MachineVitals(int health, float staggerTime)
    {
        _health = new Vitality(health);
        _staggerTime = staggerTime;
    }

    /// <summary>Broken by the pilgrim: folding down, then gone.</summary>
    public bool Broken => _breaking >= 0f;

    /// <summary>The hit's flash, 1 as a blow lands, fading to 0.</summary>
    public float Flash { get; private set; }

    /// <summary>How far the fold has gone, 0 to 1, eased (smoothstep); 0 until broken.</summary>
    public float Folded
    {
        get
        {
            float k = Math.Clamp(MathF.Max(_breaking, 0f) / BreakTime, 0f, 1f);
            return k * k * (3f - 2f * k);
        }
    }

    /// <summary>The fold is over: the machine is gone.</summary>
    public bool Gone => _breaking >= BreakTime;

    /// <summary>A blow: the flash, the stagger, the damage. Returns true if this blow broke it (never once broken).</summary>
    public bool Hit(int damage)
    {
        if (Broken)
        {
            return false;
        }

        _health.Damage(damage);
        Flash = 1f;
        _stagger = _staggerTime;
        if (_health.Dead)
        {
            _breaking = 0f;
            return true;
        }

        return false;
    }

    /// <summary>Already broken (a boss beaten before): gone at once, no folding.</summary>
    public void Vanish() => _breaking = BreakTime;

    /// <summary>
    /// One tick of thinking: the brain's intent, or, while staggered, nothing but the rousing it had (<paramref name="last"/>): the
    /// brain is stunned and the body slides to a stop. The flash fades either way.
    /// </summary>
    public Intent Think(MachineBrain brain, in Perception perception, Intent last, float dt)
    {
        Flash = MathF.Max(0f, Flash - dt * FlashFade);
        if (_stagger > 0f)
        {
            _stagger -= dt;
            return new Intent { Rouse = last.Rouse };
        }

        return brain.Think(perception, dt);
    }

    /// <summary>One tick of breaking apart (once <see cref="Broken"/>).</summary>
    public void Fold(float dt) => _breaking += dt;

    /// <summary>
    /// The velocity a blow from <paramref name="fromX"/> pushes a machine at <paramref name="x"/> with: away from it and a little up,
    /// scaled by its <paramref name="heft"/> (a flyer is lifted less). A crawler is never pushed: null.
    /// </summary>
    public static Vector2? Knockback(Locomotion locomotion, float x, float fromX, float knockback, float heft) =>
        locomotion == Locomotion.Crawl
            ? null
            : new Vector2((x >= fromX ? 1f : -1f) * knockback * heft, (locomotion == Locomotion.Hover ? 1f : 2f) * heft);
}
