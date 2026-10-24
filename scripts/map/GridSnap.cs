using System;

namespace TheDeep;

/// <summary>The kit's grid: gameplay blocks have their edges on <see cref="BlockStep"/>, kit pieces their origin on <see cref="PieceStep"/>.</summary>
public static class GridSnap
{
    /// <summary>Meters between grid lines for gameplay block edges (X, Y).</summary>
    public const float BlockStep = 0.5f;

    /// <summary>Meters between grid lines for kit piece origins.</summary>
    public const float PieceStep = 0.25f;

    private const float Tolerance = 1e-3f;

    public static bool OnGrid(float value, float step)
    {
        float cells = value / step;
        return MathF.Abs(cells - MathF.Round(cells)) * step < Tolerance;
    }

    /// <summary>Both edges of a box centered on <paramref name="center"/> with <paramref name="size"/> lie on the grid.</summary>
    public static bool EdgesOnGrid(float center, float size, float step) =>
        OnGrid(center - size * 0.5f, step) && OnGrid(center + size * 0.5f, step);
}
