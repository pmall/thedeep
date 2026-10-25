using System;
using System.Numerics;

namespace TheDeep;

/// <summary>Movement patterns of the behavior vocabulary, as desired velocities (pure).</summary>
public static class Steering
{
    /// <summary>Toward <paramref name="target"/> at up to <paramref name="speed"/>, slowing down inside <paramref name="slowRadius"/> to stop on it.</summary>
    public static Vector2 Arrive(Vector2 from, Vector2 target, float speed, float slowRadius)
    {
        Vector2 d = target - from;
        float distance = d.Length();
        if (distance < 1e-4f)
        {
            return Vector2.Zero;
        }

        float s = speed * MathF.Min(1f, distance / MathF.Max(slowRadius, 1e-3f));
        return d / distance * s;
    }

    /// <summary>Directly away from <paramref name="threat"/> along X at <paramref name="speed"/> (a walker fleeing).</summary>
    public static Vector2 FleeX(Vector2 from, Vector2 threat, float speed, int fallback = 1) =>
        new(AwaySide(from, threat, fallback) * speed, 0f);

    /// <summary>The side of <paramref name="threat"/> that <paramref name="from"/> is on: +1 right of it, -1 left.</summary>
    public static int AwaySide(Vector2 from, Vector2 threat, int fallback = 1)
    {
        float dx = from.X - threat.X;
        return MathF.Abs(dx) < 1e-3f ? (fallback < 0 ? -1 : 1) : MathF.Sign(dx);
    }

    /// <summary>A slow lazy figure eight around <paramref name="home"/> (a flyer idling), <paramref name="size"/> meters wide.</summary>
    public static Vector2 Loiter(Vector2 home, float time, float size) =>
        home + new Vector2(MathF.Sin(time * 0.31f) * size * 0.5f, MathF.Sin(time * 0.62f) * size * 0.15f);
}
