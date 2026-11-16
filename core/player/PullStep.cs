using System.Numerics;

namespace TheDeep;

/// <summary>One tick of a harpoon pull (<see cref="HarpoonPull.Step"/>): the new speed, the velocity to move at, and whether this tick reaches the anchor.</summary>
public readonly record struct PullStep(float Speed, Vector2 Velocity, bool Arriving);
