using System;
using System.Numerics;

namespace TheDeep;

/// <summary>The lattice primitive of <see cref="MeshBuilder"/>: a smooth surface through a grid of points.</summary>
public sealed partial class MeshBuilder
{
    /// <summary>
    /// A smooth surface through a grid of <paramref name="points"/> (rows × columns), for shapes no sweep makes (a pipe
    /// flattened at its lips, a curtain of ice). Normals come from the grid's own slopes, turned to the side
    /// <paramref name="outward"/> points to (also the normal where the grid is degenerate). U runs down the rows, V along
    /// the columns: <paramref name="uv"/> gives both, on a patch of <paramref name="patchSize"/>. With
    /// <paramref name="wraps"/>, the last column repeats the first (a closed girth) and slopes are taken across the seam.
    /// </summary>
    public void Lattice(Vector3[,] points, Vector3[,] outward, Vector2[,] uv, Vector2 patchSize, bool wraps)
    {
        int rows = points.GetLength(0), columns = points.GetLength(1);
        var index = new int[rows, columns];
        for (int j = 0; j < rows; j++)
        {
            for (int k = 0; k < columns; k++)
            {
                Vector3 along = points[Math.Min(j + 1, rows - 1), k] - points[Math.Max(j - 1, 0), k];
                int previous = k - 1, next = k + 1;
                if (wraps)
                {
                    previous = k == 0 ? columns - 2 : previous;
                    next = k == columns - 1 ? 1 : next;
                }

                Vector3 across = points[j, Math.Min(next, columns - 1)] - points[j, Math.Max(previous, 0)];
                Vector3 normal = Vector3.Cross(along, across);
                Vector3 hint = outward[j, k];
                if (normal.LengthSquared() < 1e-12f)
                {
                    normal = hint;
                }
                else if (Vector3.Dot(normal, hint) < 0f)
                {
                    normal = -normal;
                }

                if (along.LengthSquared() < 1e-12f)
                {
                    along = Vector3.Cross(across, normal);
                }

                if (along.LengthSquared() < 1e-12f)
                {
                    along = Vector3.Cross(normal, MathF.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
                }

                if (across.LengthSquared() < 1e-12f)
                {
                    across = Vector3.Cross(normal, along);
                }

                index[j, k] = Vertex(points[j, k], normal, along, across, uv[j, k], patchSize);
            }
        }

        for (int j = 0; j + 1 < rows; j++)
        {
            for (int k = 0; k + 1 < columns; k++)
            {
                Quad(index[j, k], index[j, k + 1], index[j + 1, k + 1], index[j + 1, k]);
            }
        }
    }
}
