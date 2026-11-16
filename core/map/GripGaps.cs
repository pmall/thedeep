using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// The room rule that keeps <see cref="ReachPlanner"/>'s grip walk exact: no gap the game's grip may bridge between two metal solids.
/// The game's grip (<c>GripState</c>) goes on over a break in its surface while the body stays supported by the metal at one end and
/// its middle probe finds no rock, so a gap narrower than the body is crossed; the planner ends the walk at any gap. A room
/// whose metal has such a gap is a problem to fix (build the solids touching, or leave a gap no grip crosses), so the planner never
/// under-claims there.
/// </summary>
public static class GripGaps
{
    /// <summary>
    /// The widest gap held a problem, meters: the body's width (the capsule rests on the metal at one end across any gap narrower
    /// than it, the game's bridge) plus <see cref="Margin"/>.
    /// </summary>
    public static float Widest(float bodyWidth) => bodyWidth + Margin;

    /// <summary>Slack on the body's width, meters: the capsule is rounder than the planner's box, and a hair of slack costs no room anything.</summary>
    public const float Margin = 0.25f;

    /// <summary>How far apart two faces may lie and still be one plane, meters (the same tolerance as <see cref="Spans.Tolerance"/>).</summary>
    private const float SamePlane = Spans.Tolerance;

    /// <summary>
    /// The problems among <paramref name="metal"/>: each pair of solids whose faces look the same way in one plane (two tops, two
    /// bottoms, two west faces, two east faces) with a gap along that plane above zero and at most <see cref="Widest"/>.
    /// </summary>
    public static IEnumerable<string> Problems(IReadOnlyList<Box> metal, float bodyWidth)
    {
        float widest = Widest(bodyWidth);
        for (int i = 0; i < metal.Count; i++)
        {
            for (int j = i + 1; j < metal.Count; j++)
            {
                Box a = metal[i];
                Box b = metal[j];
                bool floorOrCeiling = Near(a.MaxY, b.MaxY) || Near(a.MinY, b.MinY);
                bool wall = Near(a.MinX, b.MinX) || Near(a.MaxX, b.MaxX);
                float gapX = Math.Max(a.MinX - b.MaxX, b.MinX - a.MaxX);
                float gapY = Math.Max(a.MinY - b.MaxY, b.MinY - a.MaxY);
                if (floorOrCeiling && Bridgeable(gapX, widest))
                {
                    yield return Describe(a, b, gapX);
                }
                else if (wall && Bridgeable(gapY, widest))
                {
                    yield return Describe(a, b, gapY);
                }
            }
        }
    }

    private static bool Near(float x, float y) => Math.Abs(x - y) <= SamePlane;

    private static bool Bridgeable(float gap, float widest) => gap > SamePlane && gap <= widest;

    private static string Describe(Box a, Box b, float gap) =>
        $"metal solids {a} and {b} face each other in one plane across a gap of {gap:0.##} m: the grip may bridge it and the reach planner would not (ReachPlanner, Mag-grip); build them touching";
}
