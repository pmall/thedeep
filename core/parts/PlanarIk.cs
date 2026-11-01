using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Inverse kinematics in the side plane (Godot-free). Angles are rotations around Z from the rest pose
/// of a segment hanging straight down: positive swings its end forward (+X), as the camera sees a body
/// facing right.
/// </summary>
public static class PlanarIk
{
    /// <summary>
    /// Absolute angles of a two-segment limb (thigh and shin, upper arm and forearm) reaching from
    /// <paramref name="root"/> toward <paramref name="target"/>. The middle joint bends toward +X when
    /// <paramref name="bend"/> is positive (a knee), toward -X when negative (an elbow). A target out of reach
    /// straightens the limb toward it.
    /// </summary>
    public static (float Upper, float Lower) TwoBone(Vector2 root, Vector2 target, float upper, float lower, float bend)
    {
        Vector2 d = target - root;
        float reach = upper + lower;
        float distance = Math.Clamp(d.Length(), MathF.Abs(upper - lower) + 1e-4f, reach * 0.9999f);
        // Direction of the target, measured from straight down.
        float toward = d.LengthSquared() > 1e-12f ? MathF.Atan2(d.X, -d.Y) : 0f;
        float atRoot = MathF.Acos(Math.Clamp((upper * upper + distance * distance - lower * lower) / (2f * upper * distance), -1f, 1f));
        float atEnd = MathF.Acos(Math.Clamp((lower * lower + distance * distance - upper * upper) / (2f * lower * distance), -1f, 1f));
        float side = bend >= 0f ? 1f : -1f;
        return (toward + side * atRoot, toward - side * atEnd);
    }

    /// <summary>Where a segment of <paramref name="length"/> at absolute angle <paramref name="angle"/> ends, starting at <paramref name="start"/>.</summary>
    public static Vector2 End(Vector2 start, float angle, float length) =>
        start + new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * length;

    /// <summary><paramref name="v"/> rotated counterclockwise (as the camera sees it) by <paramref name="angle"/>.</summary>
    public static Vector2 Rotate(Vector2 v, float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
    }
}
