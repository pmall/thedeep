using System;

namespace TheDeep;

/// <summary>
/// The lattice cell a point falls in, for a pattern that puts one thing (a pit, a bubble, a crystal) in each cell of a
/// tile-sized lattice (<see cref="TileNoise.Locate"/>). <paramref name="X"/> and <paramref name="Y"/> are the point in lattice
/// cells; <paramref name="Cx"/> and <paramref name="Cy"/> the cell's corner; <paramref name="Wx"/> and <paramref name="Wy"/> the
/// same cell wrapped into the tile, which is what the cell's hash is taken at, so the tile repeats.
/// </summary>
public readonly record struct LatticeCell(double X, double Y, int Cx, int Cy, int Wx, int Wy)
{
    /// <summary>
    /// The point as seen from the center of this cell's thing, in cells: the center is the cell's middle moved by up to half of
    /// <paramref name="room"/> (a share of the cell) either way, by <paramref name="hashX"/> and <paramref name="hashY"/> (in
    /// [0, 1)), so a thing of radius <c>r</c> stays inside its cell when <c>room</c> is <c>1 - 2r</c>.
    /// </summary>
    public (double X, double Y) OffsetFromCenter(double hashX, double hashY, double room) =>
        (X - (Cx + 0.5 + ((hashX - 0.5) * room)), Y - (Cy + 0.5 + ((hashY - 0.5) * room)));

    /// <summary>How far the point is from that center, in cells (<see cref="OffsetFromCenter"/>).</summary>
    public double DistanceToCenter(double hashX, double hashY, double room)
    {
        (double dx, double dy) = OffsetFromCenter(hashX, hashY, room);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
