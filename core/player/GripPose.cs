using System;
using System.Numerics;

namespace TheDeep;

/// <summary>A pose on a surface: where the feet are and which way is up (the surface's normal).</summary>
public readonly record struct GripPose(Vector2 Feet, Vector2 Up);
