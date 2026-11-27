using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Harpoon reachability for <see cref="ReachPlanner"/>: finds every mooring ring in range and clear sight from a body center,
/// and computes the zip-release moves from each reached anchor.
/// </summary>
internal sealed class HarpoonReach
{
    public HarpoonReach(HarpoonLimits limits, List<Vector2> anchors)
    {
        Limits = limits;
        Anchors = anchors;
    }

    public HarpoonLimits Limits { get; }
    public List<Vector2> Anchors { get; }

    /// <summary>Enqueues every unreached anchor in range and clear sight from <paramref name="center"/>.</summary>
    public void Fire(Vector2 center, SolidSpace space, HashSet<int> reachedAnchors, Queue<(Vector2 Anchor, Vector2 From)> anchorQueue)
    {
        for (int a = 0; a < Anchors.Count; a++)
        {
            Vector2 anchor = Anchors[a];
            if (HarpoonAim.InReach(Vector2.Distance(center, anchor), Limits.Range, Limits.MinDistance)
                && !reachedAnchors.Contains(a)
                && space.InSight(center, anchor))
            {
                reachedAnchors.Add(a);
                anchorQueue.Enqueue((anchor, center));
            }
        }
    }

    /// <summary>Pulled from <paramref name="from"/> to <paramref name="anchor"/> (the body's center), then letting go there.</summary>
    public void LetGo(Vector2 anchor, Vector2 from, SolidSpace space, IReadOnlyList<Box> triggers, HashSet<int> reachedTriggers,
        Action<Vector2, Vector2, int, int, int> launch)
    {
        float distance = Vector2.Distance(from, anchor);
        float speed = MathF.Min(Limits.ZipSpeed, MathF.Sqrt(2f * Limits.ZipAcceleration * distance));
        Vector2 release = HarpoonAim.Release(Vector2.Normalize(anchor - from), speed, Limits.ExitSpeedFactor, Limits.ExitLift);
        Vector2 feet = anchor - new Vector2(0f, space.Limits.Height * 0.5f);
        space.Touch(feet, triggers, reachedTriggers);
        foreach (int dir in new[] { -1, 0, 1 })
        {
            foreach (int jump in new[] { 0, -1 })
            {
                foreach (int dash in new[] { -1, 6, 14 })
                {
                    launch(feet, release, dir, jump, dash);
                }
            }
        }
    }
}
