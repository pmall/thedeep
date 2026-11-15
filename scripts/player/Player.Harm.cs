using System;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

// The pilgrim's hurts: blows, burns and the footing a burn sets it back on.
public partial class Player
{
    /// <summary>Raised when a blow hurts the pilgrim (health already taken).</summary>
    public event Action? Hurt;

    /// <summary>Raised when the last health is lost.</summary>
    public event Action? Died;

    /// <summary>Raised when slag, or heat it isn't plated for, burns the pilgrim and it lives (<see cref="Scorch"/>): it is to be set back on its <see cref="Footing"/>.</summary>
    public event Action? Scorched;

    /// <summary>
    /// The last ground the pilgrim stood on, upright and in control, clear of any slag or heat that burns it: where a burn sets it back
    /// (<see cref="Scorch"/>). Set again by every respawn and arrival.
    /// </summary>
    public Vector3 Footing { get; private set; }

    /// <summary>The last physics frame something that burns it was near the body (<see cref="NearBurn"/>).</summary>
    private ulong _burnNear;

    /// <summary>
    /// A blow from <paramref name="from"/> (on the play plane): takes <paramref name="damage"/> health unless in the
    /// grace period, holds the pilgrim still a moment (the hurt stop, so the blow lands), then throws it back and up, and briefly takes
    /// control away. Returns whether it hurt.
    /// </summary>
    public bool TakeHit(int damage, NVector2 from)
    {
        if (!Vitality.Damage(damage))
        {
            return false;
        }

        ChangeState(States.Fall);
        Velocity = MoveRules.Knockback(GlobalPosition.X, from.X, Limits).OnPlane();
        HitStop(Tuning.HurtStop);
        Control.Hurt();
        Visual.Hurt();
        Hurt?.Invoke();
        if (Vitality.Dead)
        {
            Died?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Burned by slag, or by heat without Heat plating (`docs/gameplay.md#slag`): <paramref name="damage"/> health taken unless in the grace period, held where it is
    /// in its recoil, control gone, then <see cref="Scorched"/> (the session sets it back on its <see cref="Footing"/>) or
    /// <see cref="Died"/>. Returns whether it hurt.
    /// </summary>
    public bool Scorch(int damage)
    {
        bool hurt = Vitality.Damage(damage);
        ChangeState(States.Fall);
        Velocity = Vector3.Zero;
        Control.Hurt();
        Visual.Hurt();
        if (hurt)
        {
            Hurt?.Invoke();
        }

        if (Vitality.Dead)
        {
            Died?.Invoke();
        }
        else
        {
            Scorched?.Invoke();
        }

        return hurt;
    }

    /// <summary>Slag or burning heat is near the body this tick (within its margin): the ground here is no <see cref="Footing"/>.</summary>
    public void NearBurn() => _burnNear = Engine.GetPhysicsFrames();
}
