using System.Numerics;

namespace TheDeep;

/// <summary>
/// What one strike is (<see cref="StrikeKind"/>), as numbers from the tuning: how far it reaches along <see cref="Toward"/> (the body's
/// forward, or down) and how wide it is across, the damage it adds to a blow's, how hard it throws back (m/s), the hit stop it gives
/// on landing (seconds the pilgrim and what it struck hold still), the upward speed a hit gives the pilgrim (a down strike's
/// bounce; 0 for none), and how the landing shows: how far it kicks the camera along the blow (meters) and the strength of its
/// sparks (0 to 1, a full burst).
/// </summary>
public readonly record struct StrikeShape(
    StrikeKind Kind, float Reach, float Width, Vector2 Toward, int ExtraDamage, float Knockback, float HitStop, float Bounce,
    float Kick, float Sparks);
