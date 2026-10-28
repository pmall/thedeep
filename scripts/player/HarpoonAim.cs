using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Which anchor the Harpoon fires at, pure (docs/gameplay.md). Anchors within <c>range</c> of the chest, not
/// closer than <c>minDistance</c>, and in clear sight are candidates. Held input aims: only anchors within
/// <see cref="AimCone"/> of it count, the best lined up first. With no input the pilgrim looks ahead and up:
/// anchors in front beat anchors above, which beat anchors behind; nearer ones win within a group.
/// </summary>
public static class HarpoonAim
{
    /// <summary>Half-angle (radians) of the cone around held input inside which an anchor can be picked.</summary>
    public const float AimCone = 1.2f;

    /// <summary>
    /// The index of the anchor to fire at, or −1. <paramref name="aim"/> is the held input (screen axes, zero for none);
    /// <paramref name="facing"/> +1 or −1; <paramref name="clear"/> says whether an anchor is in sight from <paramref name="chest"/>.
    /// </summary>
    public static int Pick(Vector2 chest, Vector2 aim, int facing, IReadOnlyList<Vector2> anchors, float range, float minDistance,
        Func<int, bool> clear, int skip = -1)
    {
        int best = -1;
        float bestScore = float.MaxValue;
        bool aiming = aim != Vector2.Zero;
        Vector2 aimDir = aiming ? Vector2.Normalize(aim) : Vector2.Zero;
        for (int i = 0; i < anchors.Count; i++)
        {
            Vector2 to = anchors[i] - chest;
            float distance = to.Length();
            if (i == skip || distance > range || distance < minDistance)
            {
                continue;
            }

            Vector2 dir = to / distance;
            float score;
            if (aiming)
            {
                float angle = MathF.Acos(Math.Clamp(Vector2.Dot(dir, aimDir), -1f, 1f));
                if (angle > AimCone)
                {
                    continue;
                }

                score = angle * 10f + distance / range;
            }
            else
            {
                // In front (a third of the way out or more), above, behind: each group beats the next.
                bool ahead = dir.X * facing > 0.33f;
                bool above = !ahead && dir.Y > 0.5f;
                score = (ahead ? 0f : above ? 10f : 20f) + distance / range;
            }

            if (score < bestScore && clear(i))
            {
                best = i;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// Velocity on letting go of an anchor reached along <paramref name="direction"/> (unit): the zip's speed scaled by
    /// <paramref name="exitFactor"/>; unless the pull went steeply down, with at least <paramref name="lift"/> upward, so the
    /// pilgrim rises over what the anchor hangs from.
    /// </summary>
    public static Vector2 Release(Vector2 direction, float zipSpeed, float exitFactor, float lift)
    {
        Vector2 v = direction * zipSpeed * exitFactor;
        return direction.Y < -0.5f ? v : v with { Y = MathF.Max(v.Y, lift) };
    }
}
