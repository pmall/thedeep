namespace TheDeep;

/// <summary>
/// A lift on the play plane: its platform at the lower stop (a solid there), carrying whoever stands on it <see cref="Rise"/> up.
/// </summary>
public readonly record struct LiftPath(Box Platform, float Rise);
